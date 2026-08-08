using System.Reflection;

// The one version of BetterTerminal. Every project in the solution links this file, so a release is
// a single edit here: the launcher's own resource in BetterTerminal.Bootstrap\Bootstrap.rc is the
// only other place a version number is written down, and tools\build.ps1 checks that the two agree.
//
// The copy under the user profile updates itself against this number - see Services\SelfInstall.cs.
[assembly: AssemblyVersion("1.4.7.0")]
[assembly: AssemblyFileVersion("1.4.7.0")]
[assembly: AssemblyInformationalVersion("1.4.4 BETA")]
