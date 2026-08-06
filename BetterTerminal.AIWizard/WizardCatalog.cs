using System.Collections.Generic;

namespace BetterTerminal.AIWizard
{
    /// <summary>
    /// The menu flows, one list of steps per engine, transcribed from the ai.bat launcher. Each
    /// option carries the exact flag it adds, so the wizard only ever assembles strings the agent
    /// itself documents - it invents nothing. A prompt entered on the last step is appended as the
    /// trailing text the agent starts on.
    ///
    /// This is a faithful port of the launcher's core flows. It deliberately does not include the
    /// launcher's online model-list refresh, its checksum update gate or its account setup helper -
    /// those are update tooling, not part of building and running a command.
    /// </summary>
    public static class WizardCatalog
    {
        public static IList<WizardStep> StepsFor(AiEngine engine)
        {
            switch (engine)
            {
                case AiEngine.Codex: return Codex();
                case AiEngine.Gemini: return Gemini();
                case AiEngine.Antigravity: return Antigravity();
                default: return Claude();
            }
        }

        private static WizardChoice Choice(char key, string label, string fragment)
        {
            return new WizardChoice(key, label, fragment);
        }

        private static IList<WizardStep> Claude()
        {
            return new List<WizardStep>
            {
                WizardStep.Model("Model", AiEngine.Claude, "--model"),

                WizardStep.Menu("Reasoning effort",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "Low", "--effort low"),
                    Choice('3', "Medium", "--effort medium"),
                    Choice('4', "High", "--effort high"),
                    Choice('5', "Extra high", "--effort xhigh"),
                    Choice('6', "Maximum", "--effort max")),

                WizardStep.Menu("Permission mode",
                    Choice('1', "Manual (default)", string.Empty),
                    Choice('2', "Accept edits", "--permission-mode acceptEdits"),
                    Choice('3', "Plan", "--permission-mode plan"),
                    Choice('4', "Auto", "--permission-mode auto"),
                    Choice('5', "Skip all prompts", "--dangerously-skip-permissions")
                        .WithHint("no confirmation is asked")),

                WizardStep.Menu("Session",
                    Choice('1', "New", string.Empty),
                    Choice('2', "Continue last", "-c"),
                    Choice('3', "Resume specific", "-r {0}").AskingFor("Session id or name"),
                    Choice('4', "New named", "-n {0}").AskingFor("Session name"),
                    Choice('5', "Fork last", "-c --fork-session")),

                WizardStep.Menu("Logging",
                    Choice('1', "Normal", string.Empty),
                    Choice('2', "Verbose", "--verbose"),
                    Choice('3', "Debug", "--debug")),

                WizardStep.Menu("Additional working directory",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Add a directory", "--add-dir {0}").AskingFor("Directory path")),

                WizardStep.Menu("System prompt",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "Append", "--append-system-prompt {0}").AskingFor("Text to append"),
                    Choice('3', "Replace", "--system-prompt {0}").AskingFor("Replacement text")),

                WizardStep.Menu("MCP configuration",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Load a config file", "--mcp-config {0}").AskingFor("Config file path"),
                    Choice('3', "Strict, from a file",
                        "--strict-mcp-config --mcp-config {0}").AskingFor("Config file path")),

                WizardStep.Menu("Tools",
                    Choice('1', "All", string.Empty),
                    Choice('2', "A specific set", "--tools {0}").AskingFor("Tool list"),
                    Choice('3', "None", "--tools \"\"")),

                WizardStep.Menu("Chrome integration",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "Enable", "--chrome"),
                    Choice('3', "Disable", "--no-chrome")),

                WizardStep.Menu("Worktree",
                    Choice('1', "None", string.Empty),
                    Choice('2', "New", "-w"),
                    Choice('3', "New named", "-w {0}").AskingFor("Worktree name")),

                WizardStep.Menu("Startup mode",
                    Choice('1', "Normal", string.Empty),
                    Choice('2', "Bare", "--bare"),
                    Choice('3', "Safe mode", "--safe-mode")),

                WizardStep.Menu("Editor connection",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Auto-connect", "--ide")),

                WizardStep.Menu("Initial prompt",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Enter a prompt", "{0}").AskingFor("Prompt"))
            };
        }

        private static IList<WizardStep> Codex()
        {
            return new List<WizardStep>
            {
                WizardStep.Menu("Session",
                    Choice('1', "New", string.Empty),
                    Choice('2', "Resume (pick)", "resume"),
                    Choice('3', "Resume last", "resume --last"),
                    Choice('4', "Fork (pick)", "fork"),
                    Choice('5', "Fork last", "fork --last")),

                WizardStep.Model("Model", AiEngine.Codex, "-m"),

                WizardStep.Menu("Approval and sandbox",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "YOLO", "--yolo").WithHint("no approval, full access"),
                    Choice('3', "Full access", "-s danger-full-access -a never"),
                    Choice('4', "Workspace write", "-s workspace-write -a on-request"),
                    Choice('5', "Read only", "-s read-only -a untrusted")),

                WizardStep.Menu("Web search",
                    Choice('1', "Off", string.Empty),
                    Choice('2', "On", "--search")),

                WizardStep.Menu("Working directory",
                    Choice('1', "Current", string.Empty),
                    Choice('2', "Custom", "-C {0}").AskingFor("Directory path")),

                WizardStep.Menu("Initial prompt",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Enter a prompt", "{0}").AskingFor("Prompt"))
            };
        }

        private static IList<WizardStep> Gemini()
        {
            return new List<WizardStep>
            {
                WizardStep.Model("Model", AiEngine.Gemini, "-m"),

                WizardStep.Menu("Approval mode",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "YOLO", "--yolo"),
                    Choice('3', "Auto-edit", "--approval-mode auto_edit"),
                    Choice('4', "Plan", "--approval-mode plan")),

                WizardStep.Menu("Sandbox",
                    Choice('1', "Off", string.Empty),
                    Choice('2', "On", "--sandbox")),

                WizardStep.Menu("Session",
                    Choice('1', "New", string.Empty),
                    Choice('2', "Resume latest", "--resume latest"),
                    Choice('3', "Resume by index", "--resume {0}").AskingFor("Session index")),

                WizardStep.Menu("Worktree",
                    Choice('1', "None", string.Empty),
                    Choice('2', "New", "-w"),
                    Choice('3', "New named", "-w {0}").AskingFor("Worktree name")),

                WizardStep.Menu("Additional directories",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Include a list", "--include-directories {0}").AskingFor("Comma-separated paths")),

                WizardStep.Menu("Debug",
                    Choice('1', "Off", string.Empty),
                    Choice('2', "On", "--debug")),

                WizardStep.Menu("Initial prompt",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Interactive", "-i {0}").AskingFor("Prompt"),
                    Choice('3', "Headless", "-p {0}").AskingFor("Prompt"))
            };
        }

        private static IList<WizardStep> Antigravity()
        {
            return new List<WizardStep>
            {
                WizardStep.Menu("Session",
                    Choice('1', "New", string.Empty),
                    Choice('2', "Continue recent", "-c"),
                    Choice('3', "Resume by id", "--conversation {0}").AskingFor("Conversation id")),

                WizardStep.Menu("Permissions",
                    Choice('1', "Default", string.Empty),
                    Choice('2', "Skip permissions", "--dangerously-skip-permissions")),

                WizardStep.Menu("Sandbox",
                    Choice('1', "Off", string.Empty),
                    Choice('2', "On", "--sandbox")),

                WizardStep.Menu("Additional directory",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Add a directory", "--add-dir {0}").AskingFor("Directory path")),

                WizardStep.Menu("Initial prompt",
                    Choice('1', "None", string.Empty),
                    Choice('2', "Interactive", "-i {0}").AskingFor("Prompt"),
                    Choice('3', "Headless", "-p {0}").AskingFor("Prompt"))
            };
        }
    }
}
