// A small terminal renderer, written by hand so the web view carries no third-party code. It never
// parses VT: the desktop already turned the session's bytes into a grid of coloured cells, and this
// only draws that grid to a canvas and sends the keys back. What it draws is exactly what the desktop
// draws, which is what makes the two one terminal.
//
// Incoming frames only update a model and ask for a repaint; the drawing happens on an animation
// frame. Painting straight out of the socket would let a burst of output starve the main thread.
(function () {
  "use strict";

  var canvas = document.getElementById("screen");
  var ctx = canvas.getContext("2d", { alpha: false });
  var kbd = document.getElementById("kbd");
  var statusEl = document.getElementById("status");
  var stage = document.getElementById("stage");

  var cols = 80, rows = 24;
  var fg = "#cccccc", bg = "#0c0c0c";
  var fontPx = 15, cellW = 9, cellH = 20;
  var dpr = Math.max(1, window.devicePixelRatio || 1);

  var grid = [];
  var cx = 0, cy = 0, cur = 1, drawnCx = 0, drawnCy = 0;
  var ws = null;

  var dirty = {};
  var relayout = true;
  var scheduled = false;

  var FONT = '15px "Cascadia Mono", "Consolas", monospace';

  function colour(value, fallback) {
    if (!value) {
      return fallback;
    }
    var r = (value >> 16) & 255, g = (value >> 8) & 255, b = value & 255;
    return "rgb(" + r + "," + g + "," + b + ")";
  }

  function measure() {
    ctx.font = FONT;
    cellW = Math.max(1, Math.ceil(ctx.measureText("M").width));
    cellH = Math.ceil(fontPx * 1.35);
  }

  function layout() {
    canvas.width = cols * cellW * dpr;
    canvas.height = rows * cellH * dpr;

    var scale = Math.min(stage.clientWidth / (cols * cellW), stage.clientHeight / (rows * cellH));
    if (!isFinite(scale) || scale <= 0) {
      scale = 1;
    }
    canvas.style.width = Math.floor(cols * cellW * scale) + "px";
    canvas.style.height = Math.floor(rows * cellH * scale) + "px";

    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.font = FONT;
    ctx.textBaseline = "top";

    ctx.fillStyle = bg;
    ctx.fillRect(0, 0, cols * cellW, rows * cellH);
    for (var y = 0; y < rows; y++) {
      drawRow(y);
    }
    drawCursor();
  }

  function drawRow(y) {
    ctx.fillStyle = bg;
    ctx.fillRect(0, y * cellH, cols * cellW, cellH);
    var row = grid[y];
    if (!row) {
      return;
    }
    for (var x = 0; x < cols; x++) {
      var back = colour(row.b[x], null);
      if (back) {
        ctx.fillStyle = back;
        ctx.fillRect(x * cellW, y * cellH, cellW, cellH);
      }
      var code = row.ch[x] || 32;
      if (code !== 32) {
        ctx.fillStyle = colour(row.f[x], fg);
        ctx.fillText(String.fromCharCode(code), x * cellW, y * cellH);
      }
    }
  }

  function drawCursor() {
    if (!cur) {
      return;
    }
    ctx.fillStyle = "rgba(232,192,106,0.7)";
    ctx.fillRect(cx * cellW, cy * cellH, cellW, cellH);
    var row = grid[cy];
    if (row) {
      var code = row.ch[cx] || 32;
      if (code !== 32) {
        ctx.fillStyle = bg;
        ctx.fillText(String.fromCharCode(code), cx * cellW, cy * cellH);
      }
    }
    drawnCx = cx;
    drawnCy = cy;
  }

  function schedule() {
    if (!scheduled) {
      scheduled = true;
      requestAnimationFrame(render);
    }
  }

  function render() {
    scheduled = false;

    if (relayout) {
      relayout = false;
      measure();
      layout();
      return;
    }

    for (var key in dirty) {
      if (dirty.hasOwnProperty(key)) {
        drawRow(+key);
      }
    }
    dirty = {};

    if (drawnCy !== cy || drawnCx !== cx) {
      drawRow(drawnCy);
    }
    drawCursor();
  }

  function apply(msg) {
    if (msg.t === "i") {
      cols = msg.cols;
      rows = msg.rows;
      fg = colour(msg.fg, "#cccccc");
      bg = colour(msg.bg, "#0c0c0c");
      grid = [];
      dirty = {};
      relayout = true;
      schedule();
      return;
    }

    if (msg.t === "f") {
      for (var i = 0; i < msg.rows.length; i++) {
        var r = msg.rows[i];
        grid[r.y] = { ch: r.ch, f: r.f, b: r.b };
        dirty[r.y] = 1;
      }
      cx = msg.cx;
      cy = msg.cy;
      cur = msg.cur;
      schedule();
    }
  }

  function connect() {
    var scheme = location.protocol === "https:" ? "wss" : "ws";
    ws = new WebSocket(scheme + "://" + location.host + "/ws");
    ws.onopen = function () { statusEl.textContent = "connected"; };
    ws.onclose = function () {
      statusEl.textContent = "disconnected";
      setTimeout(connect, 1500);
    };
    ws.onmessage = function (event) {
      apply(JSON.parse(event.data));
    };
  }

  function send(text) {
    if (ws && ws.readyState === 1) {
      ws.send(text);
    }
  }

  var special = {
    Enter: 1, Backspace: 1, Tab: 1, Escape: 1,
    ArrowUp: 1, ArrowDown: 1, ArrowLeft: 1, ArrowRight: 1,
    Home: 1, End: 1, PageUp: 1, PageDown: 1, Delete: 1, Insert: 1
  };

  kbd.addEventListener("keydown", function (e) {
    if (special[e.key]) {
      send("k" + e.key);
      e.preventDefault();
      return;
    }
    if ((e.ctrlKey || e.metaKey) && e.key.length === 1) {
      var c = e.key.toLowerCase().charCodeAt(0);
      if (c >= 97 && c <= 122) {
        send("x" + String.fromCharCode(c - 96));
        e.preventDefault();
      }
    }
  });

  // Printable and composed (mobile, IME) text arrives here; the field is cleared each time.
  kbd.addEventListener("input", function () {
    if (kbd.value) {
      send("x" + kbd.value);
      kbd.value = "";
    }
  });

  kbd.addEventListener("blur", function () { setTimeout(function () { kbd.focus(); }, 0); });
  window.addEventListener("resize", function () { relayout = true; schedule(); });
  if (window.ResizeObserver) {
    new ResizeObserver(function () { relayout = true; schedule(); }).observe(stage);
  }

  measure();
  kbd.focus();
  connect();
})();
