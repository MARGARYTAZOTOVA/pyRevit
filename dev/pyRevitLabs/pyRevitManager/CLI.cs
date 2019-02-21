﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Drawing;

using pyRevitLabs.Common;
using pyRevitLabs.CommonCLI;
using pyRevitLabs.Common.Extensions;
using pyRevitLabs.TargetApps.Revit;
using pyRevitLabs.Language.Properties;

using DocoptNet;
using NLog;
using NLog.Config;
using NLog.Targets;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Console = Colorful.Console;


// TODO: pyrevit.exe should change it's config location based on its install location %appdata% vs everything else
// when pyrevit loads but clone can not be determined (because config is somewhere else?),
// it's possible that the attachment has been made by another user and that clone is not registered with this user


namespace pyRevitManager.Views {

    public enum pyRevitManagerLogLevel {
        Quiet,
        InfoMessages,
        Debug,
    }

    class pyRevitCLI {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private const string updaterExecutive = "pyRevitUpdater.exe";

        private const string doctopUsagePatterns = @"
    Usage:
        pyrevit help
        pyrevit (-h | --help)
        pyrevit (-V | --version)
        pyrevit (blog | docs | source | youtube | support)
        pyrevit releases --help
        pyrevit releases [--notes]
        pyrevit releases latest [--pre] [--notes]
        pyrevit releases <search_pattern> [--notes]
        pyrevit releases open latest [--pre]
        pyrevit releases open <search_pattern>
        pyrevit releases download (installer | archive) latest --dest=<dest_path> [--pre]
        pyrevit releases download (installer | archive) <search_pattern> --dest=<dest_path>
        pyrevit env [--json] [--help] [--log=<log_file>]
        pyrevit clone --help
        pyrevit clone <clone_name> <deployment_name> [--dest=<dest_path>] [--source=<archive_url>] [--branch=<branch_name>] [--log=<log_file>]
        pyrevit clone <clone_name> [--dest=<dest_path>] [--source=<repo_url>] [--branch=<branch_name>] [--log=<log_file>]
        pyrevit clones [--help]
        pyrevit clones (info | open) <clone_name>
        pyrevit clones add <clone_name> <clone_path> [--log=<log_file>]
        pyrevit clones forget (--all | <clone_name>) [--log=<log_file>]
        pyrevit clones rename <clone_name> <clone_new_name> [--log=<log_file>]
        pyrevit clones delete [(--all | <clone_name>)] [--clearconfigs] [--log=<log_file>]
        pyrevit clones branch <clone_name> [<branch_name>] [--log=<log_file>]
        pyrevit clones version <clone_name> [<tag_name>] [--log=<log_file>]
        pyrevit clones commit <clone_name> [<commit_hash>] [--log=<log_file>]
        pyrevit clones origin <clone_name> --reset [--log=<log_file>]
        pyrevit clones origin <clone_name> [<origin_url>] [--log=<log_file>]
        pyrevit clones update (--all | <clone_name>) [--log=<log_file>] [--gui]
        pyrevit clones deployments <clone_name>
        pyrevit clones engines <clone_name>
        pyrevit attach --help
        pyrevit attach <clone_name> (latest | dynamosafe | <engine_version>) (<revit_year> | --installed | --attached) [--allusers] [--log=<log_file>]
        pyrevit attached [<revit_year>] [--help]
        pyrevit switch --help
        pyrevit switch <clone_name> [<revit_year>]
        pyrevit detach --help
        pyrevit detach (--all | <revit_year>) [--log=<log_file>]
        pyrevit extend --help
        pyrevit extend <extension_name> [--dest=<dest_path>] [--branch=<branch_name>] [--log=<log_file>]
        pyrevit extend (ui | lib | run) <extension_name> <repo_url> [--dest=<dest_path>] [--branch=<branch_name>] [--log=<log_file>]
        pyrevit extensions [--help]
        pyrevit extensions search <search_pattern>
        pyrevit extensions (info | help | open) <extension_name>
        pyrevit extensions delete <extension_name> [--log=<log_file>]
        pyrevit extensions origin <extension_name> --reset [--log=<log_file>]
        pyrevit extensions origin <extension_name> [<origin_url>] [--log=<log_file>]
        pyrevit extensions paths
        pyrevit extensions paths forget --all [--log=<log_file>]
        pyrevit extensions paths (add | forget) <extensions_path> [--log=<log_file>]
        pyrevit extensions (enable | disable) <extension_name> [--log=<log_file>]
        pyrevit extensions sources
        pyrevit extensions sources forget --all [--log=<log_file>]
        pyrevit extensions sources (add | forget) <source_json_or_url> [--log=<log_file>]
        pyrevit extensions update (--all | <extension_name>) [--log=<log_file>]
        pyrevit revits --help
        pyrevit revits [--installed] [--log=<log_file>]
        pyrevit revits killall [<revit_year>] [--log=<log_file>]
        pyrevit revits fileinfo <file_or_dir_path> [--csv=<output_file>]
        pyrevit revits addons
        pyrevit revits addons prepare <revit_year> [--allusers]
        pyrevit revits addons install <addon_name> <dest_path> [--allusers]
        pyrevit revits addons uninstall <addon_name>
        pyrevit run --help
        pyrevit run <script_file_or_command_name> [--revit=<revit_year>] [--purge]
        pyrevit run <script_file_or_command_name> <model_file> [--revit=<revit_year>] [--purge]
        pyrevit init --help
        pyrevit init (ui | lib | run) <extension_name> [--usetemplate] [--templates=<templates_path>]
        pyrevit init (tab | panel | panelopt | pull | split | splitpush | push | smart | command) <bundle_name> [--usetemplate] [--templates=<templates_path>]
        pyrevit caches --help
        pyrevit caches clear (--all | <revit_year>) [--log=<log_file>]
        pyrevit config --help
        pyrevit config <template_config_path> [--log=<log_file>]
        pyrevit configs --help
        pyrevit configs logs [(none | verbose | debug)] [--log=<log_file>]
        pyrevit configs allowremotedll [(enable | disable)] [--log=<log_file>]
        pyrevit configs checkupdates [(enable | disable)] [--log=<log_file>]
        pyrevit configs autoupdate [(enable | disable)] [--log=<log_file>]
        pyrevit configs rocketmode [(enable | disable)] [--log=<log_file>]
        pyrevit configs filelogging [(enable | disable)] [--log=<log_file>]
        pyrevit configs loadbeta [(enable | disable)] [--log=<log_file>]
        pyrevit configs usercanupdate [(Yes | No)] [--log=<log_file>]
        pyrevit configs usercanextend [(Yes | No)] [--log=<log_file>]
        pyrevit configs usercanconfig [(Yes | No)] [--log=<log_file>]
        pyrevit configs usagelogging
        pyrevit configs usagelogging enable (file | server) <dest_path> [--log=<log_file>]
        pyrevit configs usagelogging disable [--log=<log_file>]
        pyrevit configs outputcss [<css_path>] [--log=<log_file>]
        pyrevit configs seed [--lock] [--log=<log_file>]
        pyrevit configs <option_path> [(enable | disable)] [--log=<log_file>]
        pyrevit configs <option_path> [<option_value>] [--log=<log_file>]
        pyrevit cli --help
        pyrevit cli addshortcut <shortcut_name> <shortcut_args> [--desc=<shortcut_description>] [--allusers]
";

        public static string PrettyHelp = @"Usage: pyrevit [OPTIONS] COMMAND

pyRevit environment and clones manager

Options:
    -h --help       Show this help
    -V --version    Show version
    --verbose       Print info messages
    --debug         Print docopt options and logger debug messages
    --log           Output log messages to external log file   

Management Commands:
    env             Print environment information
    releases        Info about pyRevit releases
    clones          Manage pyRevit clones
    extensions      Manage pyRevit extensions
    configs         Manage pyRevit configurations
    attached        Manage pyRevit attachments to installed Revit
    caches          Manage pyRevit caches
    revits          Manage installed Revits
    cli             Manage this utility

Commands:
    clone           Create a clone of pyRevit on this machine
    extend          Create a clone of a third-party pyRevit extension on this machine
    attach          Attach pyRevit clone to installed Revit
    switch          Switch active pyRevit clone
    detach          Detach pyRevit clone from installed Revit
    config          Configure pyRevit for current user
    run             Run python script in Revit
    init            Init pyRevit bundle

Help Commands:
    help            Open help in default browser
    blog            Open pyRevit blog
    docs            Open pyRevit docs
    source          Open pyRevit source repo
    youtube         Open pyRevit on YouTube
    support         Open pyRevit support page

Run 'pyrevit COMMAND --help' for more information on a command.
";

        // main cli version property
        public static Version CLIVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public static void ProcessArguments(string[] args) {

            // process arguments for logging level
            var argsList = new List<string>(args);

            if (argsList.Contains("--test")) {
                argsList.Remove("--test");
                GlobalConfigs.UnderTest = true;
            }

            // detach from console if 
            if (argsList.Contains("--gui")) {
                ConsoleProvider.Detach();
            }

            // setup logger
            // process arguments for hidden debug mode switch
            pyRevitManagerLogLevel logLevel = pyRevitManagerLogLevel.InfoMessages;
            var config = new LoggingConfiguration();
            var logconsole = new ConsoleTarget("logconsole") { Layout = @"${level}: ${message} ${exception}" };
            config.AddTarget(logconsole);
            config.AddRule(LogLevel.Error, LogLevel.Fatal, logconsole);

            if (argsList.Contains("--verbose")) {
                argsList.Remove("--verbose");
                logLevel = pyRevitManagerLogLevel.InfoMessages;
                config.AddRule(LogLevel.Info, LogLevel.Info, logconsole);
            }

            if (argsList.Contains("--debug")) {
                argsList.Remove("--debug");
                logLevel = pyRevitManagerLogLevel.Debug;
                config.AddRule(LogLevel.Debug, LogLevel.Debug, logconsole);
            }


            try {
                // process docopt
                // docopt raises exception if pattern matching fails
                var arguments = new Docopt().Apply(doctopUsagePatterns, argsList, exit: false, help: false);

                // print active arguments in debug mode
                if (logLevel == pyRevitManagerLogLevel.Debug)
                    foreach (var argument in arguments.OrderBy(x => x.Key)) {
                        if (argument.Value != null && (argument.Value.IsTrue || argument.Value.IsString))
                            Console.WriteLine("{0} = {1}", argument.Key, argument.Value);
                    }

                // setup output log
                if (arguments["--log"] != null) {
                    var logfile = new FileTarget("logfile") { FileName = arguments["--log"].Value as string };
                    config.AddTarget(logfile);
                    config.AddRuleForAllLevels(logfile);

                    arguments.Remove("--log");
                }

                // config logger
                LogManager.Configuration = config;

                // get active keys for safe command extraction
                var activeKeys = ExtractEnabledKeywords(arguments);

                // now call methods based on inputs
                try {
                    ExecuteCommand(arguments, activeKeys);
                }
                catch (Exception ex) {
                    LogException(ex, logLevel);
                }

                ProcessErrorCodes();

                LogManager.Shutdown(); // Flush and close down internal threads and timers
            }
            catch {
                // when docopt fails, print help
                Console.WriteLine(PrettyHelp);
            }
        }


        private static void ExecuteCommand(IDictionary<string, ValueObject> arguments,
                                           IEnumerable<string> activeKeys) {
            // =======================================================================================================
            // $ pyrevit (-V|--version)
            // =======================================================================================================
            if (arguments["--version"].IsTrue || arguments["-V"].IsTrue) {
                Console.WriteLine(string.Format(StringLib.ConsoleVersionFormat, CLIVersion.ToString()));
                if (CommonUtils.CheckInternetConnection()) {
                    var latestVersion = PyRevitRelease.GetLatestCLIReleaseVersion();
                    if (latestVersion != null) {
                        logger.Debug("Latest release: {0}", latestVersion);
                        if (CLIVersion < latestVersion) {
                            Console.WriteLine(
                                string.Format(
                                    "Newer v{0} is available.\nGo to {1} to download the installer.",
                                    latestVersion,
                                    PyRevitConsts.ReleasesUrl)
                                );
                        }
                        else
                            Console.WriteLine("You have the latest version.");
                    }
                    else
                        logger.Debug("Failed getting latest release list OR no CLI releases.");
                }
            }

            // =======================================================================================================
            // $ pyrevit help
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "help")) {
                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "help" },
                                        "Open help in default browser");
                string helpUrl = string.Format(PyRevitConsts.CLIHelpUrl, CLIVersion.ToString());
                if (CommonUtils.VerifyUrl(helpUrl)) {
                    CommonUtils.OpenUrl(
                        helpUrl,
                        logErrMsg: "Can not open online help page. Try `pyrevit --help` instead"
                        );
                }
                else
                    throw new pyRevitException(
                        string.Format("Help page is not reachable for version {0}", CLIVersion.ToString())
                        );
            }

            // =======================================================================================================
            // $ pyrevit (blog | docs | source | youtube | support)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "blog"))
                CommonUtils.OpenUrl(PyRevitConsts.BlogsUrl);

            else if (VerifyCommand(activeKeys, "docs"))
                CommonUtils.OpenUrl(PyRevitConsts.DocsUrl);

            else if (VerifyCommand(activeKeys, "source"))
                CommonUtils.OpenUrl(PyRevitConsts.SourceRepoUrl);

            else if (VerifyCommand(activeKeys, "youtube"))
                CommonUtils.OpenUrl(PyRevitConsts.YoutubeUrl);

            else if (VerifyCommand(activeKeys, "support"))
                CommonUtils.OpenUrl(PyRevitConsts.SupportRepoUrl);

            // =======================================================================================================
            // $ pyrevit releases --help
            // $ pyrevit releases [--notes]
            // $ pyrevit releases latest [--pre] [--notes]
            // $ pyrevit releases <search_pattern> [--notes]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "releases")
                        || VerifyCommand(activeKeys, "releases", "latest")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "releases" },
                        title: "Info on pyRevit Releases",
                        commands: new Dictionary<string, string>() {
                                { "open",       "Open release page in default browser" },
                                { "download",   "Download installer or archive" }
                            },
                        options: new Dictionary<string, string>() {
                                { "latest",             "Match latest release only" },
                                { "<search_pattern>",   "Pattern to search releases" },
                                { "--pre",              "Include pre-releases in the search" },
                                { "--notes",            "Print release notes" }
                            }
                        );

                bool printReleaseNotes = arguments["--notes"].IsTrue;
                bool listPreReleases = arguments["--pre"].IsTrue;

                List<PyRevitRelease> releasesToList = new List<PyRevitRelease>();

                // determine latest release
                if (arguments["latest"].IsTrue) {
                    var latest = PyRevitRelease.GetLatestRelease(includePreRelease: listPreReleases);

                    if (latest == null)
                        throw new pyRevitException("Can not determine latest release.");

                    releasesToList.Add(latest);
                }
                else {
                    string searchPattern = TryGetValue(arguments, "<search_pattern>");

                    if (searchPattern != null)
                        releasesToList = PyRevitRelease.GetLatestReleases().Where(r => r.IsPyRevitRelease && (r.Name.Contains(searchPattern) || r.Tag.Contains(searchPattern))).ToList();
                    else
                        releasesToList = PyRevitRelease.GetLatestReleases().Where(r => r.IsPyRevitRelease).ToList();
                }

                foreach (var prelease in releasesToList) {
                    Console.WriteLine(prelease);
                    if (printReleaseNotes)
                        Console.WriteLine(prelease.ReleaseNotes.Indent(1));
                }
            }

            // =======================================================================================================
            // $ pyrevit releases open latest [--pre]
            // $ pyrevit releases open <search_pattern>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "releases", "open")
                        || VerifyCommand(activeKeys, "releases", "open", "latest")) {

                bool listPreReleases = arguments["--pre"].IsTrue;

                PyRevitRelease matchedRelease = null;
                // determine latest release
                if (arguments["latest"].IsTrue) {
                    matchedRelease = PyRevitRelease.GetLatestRelease(includePreRelease: listPreReleases);

                    if (matchedRelease == null)
                        throw new pyRevitException("Can not determine latest release.");
                }
                // or find first release matching given pattern
                else {
                    string searchPattern = TryGetValue(arguments, "<search_pattern>");
                    if (searchPattern != null) {
                        matchedRelease =
                            PyRevitRelease.GetLatestReleases()
                                          .Where(r => r.IsPyRevitRelease
                                                    && (r.Name.Contains(searchPattern) || r.Tag.Contains(searchPattern)))
                                          .ToList()
                                          .First();
                        if (matchedRelease == null)
                            throw new pyRevitException(
                                string.Format("No release matching \"{0}\" were found.", searchPattern)
                                );
                    }
                }

                CommonUtils.OpenUrl(matchedRelease.Url);
            }

            // =======================================================================================================
            // $ pyrevit releases download (installer|archive) latest --dest=<dest_path> [--pre]
            // $ pyrevit releases download (installer|archive) <search_pattern> --dest=<dest_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "releases", "download", "installer")
                        || VerifyCommand(activeKeys, "releases", "download", "archive")
                        || VerifyCommand(activeKeys, "releases", "download", "installer", "latest")
                        || VerifyCommand(activeKeys, "releases", "download", "archive", "latest")) {

                bool listPreReleases = arguments["--pre"].IsTrue;

                // get dest path
                string destPath = TryGetValue(arguments, "--dest");
                if (destPath == null)
                    throw new pyRevitException("Destination path is not specified.");

                PyRevitRelease matchedRelease = null;
                // determine latest release
                if (arguments["latest"].IsTrue) {
                    matchedRelease = PyRevitRelease.GetLatestRelease(includePreRelease: listPreReleases);

                    if (matchedRelease == null)
                        throw new pyRevitException("Can not determine latest release.");

                }
                // or find first release matching given pattern
                else {
                    string searchPattern = TryGetValue(arguments, "<search_pattern>");
                    if (searchPattern != null) {
                        matchedRelease =
                            PyRevitRelease.GetLatestReleases()
                                          .Where(r => r.IsPyRevitRelease
                                                    && (r.Name.Contains(searchPattern) || r.Tag.Contains(searchPattern)))
                                          .ToList()
                                          .First();
                    }

                    if (matchedRelease == null)
                        throw new pyRevitException(
                            string.Format("No release matching \"{0}\" were found.", searchPattern)
                            );
                }

                // grab download url
                var downloadUrl =
                    arguments["archive"].IsTrue ? matchedRelease.ArchiveUrl : matchedRelease.InstallerUrl;
                logger.Debug("Downloading release package from \"{0}\"", downloadUrl);

                // ensure destpath is to a file
                if (CommonUtils.VerifyPath(destPath))
                    destPath = Path.Combine(destPath, Path.GetFileName(downloadUrl)).NormalizeAsPath();
                logger.Debug("Saving package to \"{0}\"", destPath);

                // download file and report
                CommonUtils.DownloadFile(downloadUrl, destPath);
                Console.WriteLine(
                    string.Format("Downloaded package to \"{0}\"", destPath)
                    );
            }

            // =======================================================================================================
            // $ pyrevit env [--json] [--help] [--log=<log_file>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "env")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "env" },
                        title: "Print environment information.",
                        options: new Dictionary<string, string>() {
                                { "--json",     "Switch output format to json" },
                            }
                        );

                if (arguments["--json"].IsTrue) {

                    // collecet search paths
                    var searchPaths = new List<string>() { PyRevit.pyRevitDefaultExtensionsPath };
                    searchPaths.AddRange(PyRevit.GetRegisteredExtensionSearchPaths());

                    // collect list of lookup sources
                    var lookupSrc = new List<string>() { PyRevit.GetDefaultExtensionLookupSource() };
                    lookupSrc.AddRange(PyRevit.GetRegisteredExtensionLookupSources());

                    // create json data object
                    var jsonData = new Dictionary<string, object>() {
                        { "meta", new Dictionary<string, object>() {
                                { "version", "0.1.0"}
                            }
                        },
                        { "clones", PyRevit.GetRegisteredClones() },
                        { "attachments", PyRevit.GetAttachments() },
                        { "extensions", PyRevit.GetInstalledExtensions() },
                        { "searchPaths", searchPaths },
                        { "lookupSources", lookupSrc },
                        { "installed", RevitProduct.ListInstalledProducts() },
                        { "running", RevitController.ListRunningRevits() },
                        { "pyrevitDataDir", PyRevit.pyRevitAppDataPath },
                        { "userEnv", new Dictionary<string, object>() {
                                { "osVersion", UserEnv.GetWindowsVersion() },
                                { "execUser", string.Format("{0}\\{1}", Environment.UserDomainName, Environment.UserName) },
                                { "activeUser", UserEnv.GetLoggedInUserName() },
                                { "isAdmin", UserEnv.IsRunAsAdmin() },
                                { "userAppdata", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) },
                                { "latesFramework", UserEnv.GetInstalledDotNetVersion() },
                                { "targetPacks", UserEnv.GetInstalledDotnetTargetPacks() },
                                { "targetPacksCore", UserEnv.GetInstalledDotnetCoreTargetPacks() },
                                { "cliVersion", CLIVersion },
                            }
                        },
                    };

                    Console.WriteLine(
                        JsonConvert.SerializeObject(
                            jsonData,
                            new JsonSerializerSettings {
                                Error = delegate (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) {
                                    args.ErrorContext.Handled = true;
                                },
                                ContractResolver = new CamelCasePropertyNamesContractResolver()
                            })
                        );
                    
                }
                else {
                    PrintClones();
                    PrintAttachments();
                    PrintExtensions();
                    PrintExtensionSearchPaths();
                    PrintExtensionLookupSources();
                    PrintInstalledRevits();
                    PrintRunningRevits();
                    PrinUserEnv();
                }
            }

            // =======================================================================================================
            // $ pyrevit clone <clone_name> <deployment_name> [--dest=<dest_path>] [--source=<archive_url>] [--branch=<branch_name>] [--log=<log_file>]
            // $ pyrevit clone <clone_name> [--dest=<dest_path>] [--source=<repo_url>] [--branch=<branch_name>] [--log=<log_file>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clone")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "clone" },
                        title: "Create a clone of pyRevit on this machine",
                        options: new Dictionary<string, string>() {
                                { "<clone_name>",       "Name of this new clone" },
                                { "<deployment_name>",  "Deployment configuration to deploy from" },
                                { "--dest",             "Clone destination directory" },
                                { "--source",           "Clone source; Zip archive or git url" },
                                { "--branch",           "Branch to clone from" },
                            }
                        );

                var cloneName = TryGetValue(arguments, "<clone_name>");
                var deployName = TryGetValue(arguments, "<deployment_name>");
                if (cloneName != null) {
                    PyRevit.Clone(
                        cloneName,
                        deploymentName: deployName,
                        branchName: TryGetValue(arguments, "--branch"),
                        repoOrArchivePath: TryGetValue(arguments, "--source"),
                        destPath: TryGetValue(arguments, "--dest")
                        );
                }
            }

            // =======================================================================================================
            // $ pyrevit clones
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "clones" },
                        title: "Manage pyRevit clones",
                        commands: new Dictionary<string, string>() {
                                { "info",       "Print info about clone" },
                                { "open",       "Open clone directory in file browser" },
                                { "add",        "Register an existing clone" },
                                { "forget",     "Forget a registered clone" },
                                { "rename",     "Rename a clone" },
                                { "delete",     "Delete a clone" },
                                { "branch",     "Get/Set branch of a clone deployed from git repo" },
                                { "version",    "Get/Set version of a clone deployed from git repo" },
                                { "commit",     "Get/Set head commit of a clone deployed from git repo" },
                                { "origin",     "Get/Set origin of a clone deployed from git repo" },
                                { "update",     "Update clone to latest using the original source, deployment, and branch" },
                                { "deployment", "List deployments available in a clone" },
                                { "engines",    "List engines available in a clone" },
                            },
                        options: new Dictionary<string, string>() {
                                { "<clone_name>",       "Name of target clone" },
                                { "<clone_path>",       "Path of clone" },
                                { "<clone_new_name>",   "New name of clone" },
                                { "<branch_name>",      "Clone branch to checkout" },
                                { "<tag_name>",         "Clone tag to rebase to" },
                                { "<commit_hash>",      "Clone commit rebase to" },
                                { "<origin_url>",       "New clone remote origin url" },
                                { "--reset",            "Reset remote origin url to default" },
                                { "--clearconfigs",     "Clear pyRevit configurations." },
                                { "--all",              "All clones" },
                                { "--branch",           "Branch to clone from" },
                            }
                        );

                PrintClones();
            }

            // =======================================================================================================
            // $ pyrevit clones (info | open) <clone_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "info")
                    || VerifyCommand(activeKeys, "clones", "open")) {
                string cloneName = TryGetValue(arguments, "<clone_name>");
                PyRevitClone clone = PyRevit.GetRegisteredClone(cloneName);
                if (clone != null) {
                    if (arguments["info"].IsTrue) {
                        PrintHeader("Clone info");
                        Console.WriteLine(clone);
                    }
                    else
                        CommonUtils.OpenInExplorer(clone.ClonePath);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones add <clone_name> <clone_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "add")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                string clonePath = TryGetValue(arguments, "<clone_path>");
                if (clonePath != null)
                    PyRevit.RegisterClone(cloneName, clonePath);
            }

            // =======================================================================================================
            // $ pyrevit clones forget (--all | <clone_name>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "forget")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (arguments["--all"].IsTrue)
                    PyRevit.UnregisterAllClones();
                else {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    PyRevit.UnregisterClone(clone);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones rename <clone_name> <clone_new_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "rename")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                string cloneNewName = TryGetValue(arguments, "<clone_new_name>");
                if (cloneNewName != null) {
                    PyRevit.RenameClone(cloneName, cloneNewName);
                }
            }

            // =======================================================================================================
            // $ pyrevit clones delete [(--all | <clone_name>)] [--clearconfigs]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "delete")) {
                if (arguments["--all"].IsTrue)
                    PyRevit.DeleteAllClones(clearConfigs: arguments["--clearconfigs"].IsTrue);
                else {
                    var cloneName = TryGetValue(arguments, "<clone_name>");
                    if (cloneName != null) {
                        var clone = PyRevit.GetRegisteredClone(cloneName);
                        if (clone != null)
                            PyRevit.Delete(clone, clearConfigs: arguments["--clearconfigs"].IsTrue);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones branch <clone_name> [<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "branch")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var branchName = TryGetValue(arguments, "<branch_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (branchName != null) {
                                clone.SetBranch(branchName);
                            }
                            else {
                                Console.WriteLine(string.Format("Clone \"{0}\" is on branch \"{1}\"",
                                                                 clone.Name, clone.Branch));
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones version <clone_name> [<tag_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "version")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var tagName = TryGetValue(arguments, "<tag_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        // get version for git clones
                        if (clone.IsRepoDeploy) {
                            if (tagName != null) {
                                clone.SetTag(tagName);
                            }
                            else {
                                logger.Error("Version finder not yet implemented for git clones.");
                                // TODO: grab version from repo (last tag?)
                            }
                        }
                        // get version for other clones
                        else {
                            if (tagName != null) {
                                logger.Error("Version setter not yet implemented for clones.");
                                // TODO: set version for archive clones?
                            }
                            else {
                                Console.WriteLine(clone.ModuleVersion);
                            }
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones commit <clone_name> [<commit_hash>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "commit")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var commitHash = TryGetValue(arguments, "<commit_hash>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (commitHash != null) {
                                clone.SetCommit(commitHash);
                            }
                            else {
                                Console.WriteLine(string.Format("Clone \"{0}\" is on commit \"{1}\"",
                                                                 clone.Name, clone.Commit));
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones origin <clone_name> [<origin_url>] [--log=<log_file>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "origin")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                var originUrl = TryGetValue(arguments, "<origin_url>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        if (clone.IsRepoDeploy) {
                            if (originUrl != null || arguments["--reset"].IsTrue) {
                                string newUrl =
                                    arguments["--reset"].IsTrue ? PyRevitConsts.OriginalRepoPath : originUrl;
                                clone.SetOrigin(newUrl);
                            }
                            else {
                                Console.WriteLine(string.Format("Clone \"{0}\" origin is at \"{1}\"",
                                                                clone.Name, clone.Origin));
                            }
                        }
                        else
                            ReportCloneAsNoGit(clone);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones deployments <clone_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "deployments")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        PrintHeader(string.Format("Deployments for \"{0}\"", clone.Name));
                        foreach (var dep in clone.GetConfiguredDeployments()) {
                            Console.WriteLine(string.Format("\"{0}\" deploys:", dep.Name));
                            foreach (var path in dep.Paths)
                                Console.WriteLine("    " + path);
                            Console.WriteLine();
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones engines <clone_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "engines")) {
                var cloneName = TryGetValue(arguments, "<clone_name>");
                if (cloneName != null) {
                    var clone = PyRevit.GetRegisteredClone(cloneName);
                    if (clone != null) {
                        PrintHeader(string.Format("Deployments for \"{0}\"", clone.Name));
                        foreach (var engine in clone.GetConfiguredEngines()) {
                            Console.WriteLine(engine);
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit clones update (--all | <clone_name>) [--gui]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "clones", "update")) {
                // TODO: ask for closing running Revits

                // prepare a list of clones to be updated
                var clones = new List<PyRevitClone>();
                // separate the clone that this process might be running from
                // this is used to update this clone from outside since the dlls will be locked
                PyRevitClone myClone = null;

                // all clones
                if (arguments["--all"].IsTrue) {
                    foreach (var clone in PyRevit.GetRegisteredClones())
                        if (IsRunningInsideClone(clone))
                            myClone = clone;
                        else
                            clones.Add(clone);
                }
                // or single clone
                else {
                    var cloneName = TryGetValue(arguments, "<clone_name>");
                    if (cloneName != null) {
                        var clone = PyRevit.GetRegisteredClone(cloneName);
                        if (IsRunningInsideClone(clone))
                            myClone = clone;
                        else
                            clones.Add(clone);
                    }
                }

                // update clones that do not include this process
                foreach (var clone in clones)
                    PyRevit.Update(clone);

                // now update myClone if any, as last step
                if (myClone != null)
                    UpdateFromOutsideAndClose(myClone, showgui: arguments["--gui"].IsTrue);
            }

            // =======================================================================================================
            // $ pyrevit attach <clone_name> (latest | dynamosafe | <engine_version>) (<revit_year> | --installed | --attached) [--allusers]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "attach")
                    || VerifyCommand(activeKeys, "attach", "latest")
                    || VerifyCommand(activeKeys, "attach", "dynamosafe")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "attach" },
                        title: "Attach pyRevit clone to installed Revit",
                        options: new Dictionary<string, string>() {
                                { "<clone_name>",       "Name of target clone" },
                                { "<revit_year>",       "Revit version year e.g. 2019" },
                                { "<engine_version>",   "Engine version to be used e.g. 277" },
                                { "latest",             "Use latest engine" },
                                { "dynamosafe",         "Use latest engine that is compatible with DynamoBIM" },
                                { "--installed",        "All installed Revits" },
                                { "--attached",         "All currently attached Revits" },
                                { "--allusers",         "Attach for all users" },
                            }
                        );

                string cloneName = TryGetValue(arguments, "<clone_name>");
                var clone = PyRevit.GetRegisteredClone(cloneName);
                if (clone != null) {
                    int engineVer = 0;
                    if (arguments["dynamosafe"].IsTrue)
                        engineVer = PyRevitConsts.ConfigsDynamoCompatibleEnginerVer;
                    else {
                        string engineVersion = TryGetValue(arguments, "<engine_version>");
                        if (engineVersion != null)
                            engineVer = int.Parse(engineVersion);
                    }

                    if (arguments["--installed"].IsTrue) {
                        foreach (var revit in RevitProduct.ListInstalledProducts())
                            PyRevit.Attach(revit.FullVersion.Major,
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                    else if (arguments["--attached"].IsTrue) {
                        foreach (var attachment in PyRevit.GetAttachments())
                            PyRevit.Attach(attachment.Product.ProductYear,
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                    else {
                        string revitYear = TryGetValue(arguments, "<revit_year>");
                        if (revitYear != null)
                            PyRevit.Attach(int.Parse(revitYear),
                                           clone,
                                           engineVer: engineVer,
                                           allUsers: arguments["--allusers"].IsTrue);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit detach (--all | <revit_year>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "detach")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "detach" },
                        title: "Detach a clone from Revit.",
                        options: new Dictionary<string, string>() {
                                { "<revit_year>",   "Revit version year e.g. 2019" },
                                { "--all",          "All registered clones" },
                            }
                        );

                string revitYearValue = TryGetValue(arguments, "<revit_year>");

                if (revitYearValue != null) {
                    int revitYear = 0;
                    if (int.TryParse(revitYearValue, out revitYear))
                        PyRevit.Detach(revitYear);
                    else
                        throw new pyRevitException(string.Format("Invalid Revit year \"{0}\"", revitYearValue));
                }
                else if (arguments["--all"].IsTrue)
                    PyRevit.DetachAll();
            }

            // =======================================================================================================
            // $ pyrevit attached [<revit_year>] [--help]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "attached")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "attached" },
                        title: "List all attached clones.",
                        options: new Dictionary<string, string>() {
                                    { "<revit_year>",       "Revit version year e.g. 2019" },
                            }
                    );

                string revitYearValue = TryGetValue(arguments, "<revit_year>");
                if (revitYearValue != null) {
                    int revitYear = 0;
                    if (int.TryParse(revitYearValue, out revitYear))
                        PrintAttachments(revitYear: revitYear);
                    else
                        throw new pyRevitException(string.Format("Invalid Revit year \"{0}\"", revitYearValue));
                }
                else
                    PrintAttachments();
            }

            // =======================================================================================================
            // $ pyrevit switch <clone_name> [<revit_year>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "switch")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(
                        new List<string>() { "switch" },
                        title: "Quick switch clone of an existing attachment to another.",
                        options: new Dictionary<string, string>() {
                                    { "<clone_name>",       "Name of target clone to switch to" },
                                    { "<revit_year>",       "Revit version year e.g. 2019" },
                            }
                    );

                string cloneName = TryGetValue(arguments, "<clone_name>");
                var clone = PyRevit.GetRegisteredClone(cloneName);
                if (clone != null) {

                    string revitYearValue = TryGetValue(arguments, "<revit_year>");

                    if (revitYearValue != null) {
                        int revitYear = 0;
                        if (int.TryParse(revitYearValue, out revitYear)) {
                            var attachment = PyRevit.GetAttached(revitYear);
                            if (attachment != null)
                                PyRevit.Attach(attachment.Product.ProductYear,
                                               clone,
                                               engineVer: attachment.Engine.Version,
                                               allUsers: attachment.AllUsers);
                            else
                                throw new pyRevitException(
                                    string.Format("Can not determine existing attachment for Revit \"{0}\"",
                                                  revitYearValue)
                                    );
                        }
                        else
                            throw new pyRevitException(string.Format("Invalid Revit year \"{0}\"", revitYearValue));
                    }
                    else {
                        // read current attachments and reattach using the same config with the new clone
                        foreach (var attachment in PyRevit.GetAttachments()) {
                            PyRevit.Attach(attachment.Product.ProductYear,
                                           clone,
                                           engineVer: attachment.Engine.Version,
                                           allUsers: attachment.AllUsers);
                        }
                    }

                }
            }

            // =======================================================================================================
            // $ pyrevit extend <extension_name> [--dest=<dest_path>] [--branch=<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extend")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "extend" },
                                        "Create a clone of a third-party pyRevit extension on this machine");

                string destPath = TryGetValue(arguments, "--dest");
                string extName = TryGetValue(arguments, "<extension_name>");
                string branchName = TryGetValue(arguments, "--branch");

                var ext = PyRevit.FindRegisteredExtension(extName);
                if (ext != null) {
                    logger.Debug("Matching extension found \"{0}\"", ext.Name);
                    PyRevit.InstallExtension(ext, destPath, branchName);
                }
                else {
                    if (Errors.LatestError == ErrorCodes.MoreThanOneItemMatched)
                        throw new pyRevitException(
                            string.Format("More than one extension matches the name \"{0}\"",
                                            extName));
                    else
                        throw new pyRevitException(
                            string.Format("Not valid extension name or repo url \"{0}\"",
                                            extName));
                }
            }

            // =======================================================================================================
            // $ pyrevit extend (ui | lib | run) <extension_name> <repo_url> [--dest=<dest_path>] [--branch=<branch_name>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extend", "ui")
                        || VerifyCommand(activeKeys, "extend", "lib")
                        || VerifyCommand(activeKeys, "extend", "run")) {
                string destPath = TryGetValue(arguments, "--dest");
                string extName = TryGetValue(arguments, "<extension_name>");
                string repoUrl = TryGetValue(arguments, "<repo_url>");
                string branchName = TryGetValue(arguments, "--branch");

                PyRevitExtensionTypes extType = GetExtentionTypeFromArgument(arguments);

                PyRevit.InstallExtension(extName, extType, repoUrl, destPath, branchName);
            }

            // =======================================================================================================
            // $ pyrevit extensions [--help]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "extensions" },
                                        "Manage pyRevit extensions");

                PrintExtensions();
            }

            // =======================================================================================================
            // $ pyrevit extensions search <search_pattern>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "search")) {
                string searchPattern = TryGetValue(arguments, "<search_pattern>");
                var matchedExts = PyRevit.LookupRegisteredExtensions(searchPattern);
                PrintExtensionDefinitions(extList: matchedExts, headerPrefix: "Matched");
            }

            // =======================================================================================================
            // $ pyrevit extensions (info | help | open) <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "info")
                        || VerifyCommand(activeKeys, "extensions", "help")
                        || VerifyCommand(activeKeys, "extensions", "open")) {
                string extName = TryGetValue(arguments, "<extension_name>");
                if (extName != null) {
                    var ext = PyRevit.FindRegisteredExtension(extName);
                    if (Errors.LatestError == ErrorCodes.MoreThanOneItemMatched)
                        logger.Warn(string.Format("More than one extension matches the search pattern \"{0}\"",
                                                    extName));
                    else {
                        if (arguments["info"].IsTrue)
                            Console.WriteLine(ext.ToString());
                        else if (arguments["help"].IsTrue)
                            Process.Start(ext.Website);
                        else if (arguments["open"].IsTrue) {
                            var instExt = PyRevit.GetInstalledExtension(extName);
                            CommonUtils.OpenInExplorer(instExt.InstallPath);
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions delete <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "delete")) {
                string extName = TryGetValue(arguments, "<extension_name>");
                PyRevit.UninstallExtension(extName);
            }

            // =======================================================================================================
            // $ pyrevit extensions origin <extension_name> --reset
            // $ pyrevit extensions origin <extension_name> [<origin_url>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "origin")) {
                string extName = TryGetValue(arguments, "<extension_name>");
                var originUrl = TryGetValue(arguments, "<origin_url>");
                if (extName != null) {
                    var extension = PyRevit.GetInstalledExtension(extName);
                    if (extension != null) {
                        if (arguments["--reset"].IsTrue) {
                            var ext = PyRevit.FindRegisteredExtension(extension.Name);
                            if (ext != null)
                                extension.SetOrigin(ext.Url);
                            else
                                throw new pyRevitException(
                                    string.Format("Can not find the original url in the extension " +
                                                  "database for extension \"{0}\"",
                                                  extension.Name));
                        }
                        else if (originUrl != null) {
                            extension.SetOrigin(originUrl);
                        }
                        else {
                            Console.WriteLine(string.Format("Extension \"{0}\" origin is at \"{1}\"",
                                                            extension.Name, extension.Origin));
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions paths
            // $ pyrevit extensions paths forget --all
            // $ pyrevit extensions paths (add | forget) <extensions_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "paths"))
                PrintExtensionSearchPaths();

            else if (VerifyCommand(activeKeys, "extensions", "forget", "--all")) {
                foreach (string searchPath in PyRevit.GetRegisteredExtensionSearchPaths())
                    PyRevit.UnregisterExtensionSearchPath(searchPath);
            }

            else if (VerifyCommand(activeKeys, "extensions", "paths", "add")
                        || VerifyCommand(activeKeys, "extensions", "paths", "forget")) {
                var searchPath = TryGetValue(arguments, "<extensions_path>");
                if (searchPath != null) {
                    if (arguments["add"].IsTrue)
                        PyRevit.RegisterExtensionSearchPath(searchPath);
                    else
                        PyRevit.UnregisterExtensionSearchPath(searchPath);
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions (enable | disable) <extension_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "enable")
                        || VerifyCommand(activeKeys, "extensions", "disable")) {
                if (arguments["<extension_name>"] != null) {
                    string extName = TryGetValue(arguments, "<extension_name>");
                    if (extName != null) {
                        if (arguments["enable"].IsTrue)
                            PyRevit.EnableExtension(extName);
                        else
                            PyRevit.DisableExtension(extName);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit extensions sources
            // $ pyrevit extensions sources forget --all
            // $ pyrevit extensions sources (add | forget) <source_json_or_url>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "sources")
                        || VerifyCommand(activeKeys, "extensions", "disable"))
                PrintExtensionLookupSources();

            else if (VerifyCommand(activeKeys, "extensions", "sources", "add")
                        || VerifyCommand(activeKeys, "extensions", "sources", "forget")) {
                if (arguments["forget"].IsTrue && arguments["--all"].IsTrue)
                    PyRevit.UnregisterAllExtensionLookupSources();

                var sourceFileOrUrl = TryGetValue(arguments, "<source_json_or_url>");
                if (sourceFileOrUrl != null) {
                    if (arguments["add"].IsTrue)
                        PyRevit.RegisterExtensionLookupSource(sourceFileOrUrl);
                    else
                        PyRevit.UnregisterExtensionLookupSource(sourceFileOrUrl);
                }
            }


            // =======================================================================================================
            // $ pyrevit extensions update (--all | <extension_name>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "extensions", "update", "--all"))
                PyRevit.UpdateAllInstalledExtensions();

            else if (VerifyCommand(activeKeys, "extensions", "update")) {
                var extName = TryGetValue(arguments, "<extension_name>");
                if (extName != null) {
                    PyRevit.UpdateExtension(extName);
                }
            }

            // =======================================================================================================
            // $ pyrevit revits [--installed]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "revits" },
                                        "Manage installed and running Revits");


                if (arguments["--installed"].IsTrue)
                    PrintInstalledRevits();
                else
                    PrintRunningRevits();
            }

            // =======================================================================================================
            // $ pyrevit revits killall [<revit_year>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "killall")) {
                var revitYear = TryGetValue(arguments, "<revit_year>");
                if (revitYear != null)
                    RevitController.KillRunningRevits(int.Parse(revitYear));
                else
                    RevitController.KillAllRunningRevits();
            }

            // =======================================================================================================
            // $ pyrevit revits fileinfo <file_or_dir_path> [--csv=<output_file>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "fileinfo")) {
                var targetPath = TryGetValue(arguments, "<file_or_dir_path>");
                var outputCSV = TryGetValue(arguments, "--csv");

                // if targetpath is a single model print the model info
                if (File.Exists(targetPath))
                    if (outputCSV != null)
                        ExportModelInfoToCSV(
                            new List<RevitModelFile>() { new RevitModelFile(targetPath) },
                            outputCSV
                            );
                    else
                        PrintModelInfo(new RevitModelFile(targetPath));

                // collect all revit models
                else {
                    var models = new List<RevitModelFile>();
                    var errorList = new List<(string, string)>();

                    logger.Info(string.Format("Searching for revit files under \"{0}\"", targetPath));
                    FileAttributes attr = File.GetAttributes(targetPath);
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
                        var files = Directory.EnumerateFiles(targetPath, "*.rvt", SearchOption.AllDirectories);
                        logger.Info(string.Format(" {0} revit files found under \"{1}\"", files.Count(), targetPath));
                        foreach (var file in files) {
                            try {
                                logger.Info(string.Format("Revit file found \"{0}\"", file));
                                var model = new RevitModelFile(file);
                                models.Add(model);
                            }
                            catch (Exception ex) {
                                errorList.Add((file, ex.Message));
                            }
                        }
                    }

                    if (outputCSV != null)
                        ExportModelInfoToCSV(models, outputCSV, errorList);
                    else {
                        // report info on all files
                        foreach (var model in models) {
                            Console.WriteLine(model.FilePath);
                            PrintModelInfo(new RevitModelFile(model.FilePath));
                            Console.WriteLine();
                        }

                        // write list of files with errors
                        if (errorList.Count > 0) {
                            Console.WriteLine("An error occured while processing these files:");
                            foreach (var errinfo in errorList)
                                Console.WriteLine(string.Format("\"{0}\": {1}\n", errinfo.Item1, errinfo.Item2));
                        }
                    }

                }
            }

            // =======================================================================================================
            // $ pyrevit revits addons
            // $ pyrevit revits addons prepare <revit_year> [--allusers]
            // $ pyrevit revits addons install <addon_name> <dest_path> [--allusers]
            // $ pyrevit revits addons uninstall <addon_name>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "revits", "addons", "prepare")) {
                // setup the addon folders
                var revitYear = TryGetValue(arguments, "<revit_year>");
                if (revitYear != null)
                    Addons.PrepareAddonPath(int.Parse(revitYear), allUsers: arguments["--allusers"].IsTrue);
            }

            else if (VerifyCommand(activeKeys, "revits", "addons")
                        || VerifyCommand(activeKeys, "revits", "addons", "install")
                        || VerifyCommand(activeKeys, "revits", "addons", "uninstall")) {
                // TODO: implement revit addon manager
                logger.Error("Revit addon manager is not implemented yet");
            }

            // =======================================================================================================
            // $ pyrevit run <script_file_or_command_name> [--revit=<revit_year>] [--purge]
            // $ pyrevit run <script_file_or_command_name> <model_file> [--revit=<revit_year>] [--purge]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "run")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "run" },
                                        "Run python script in Revit");

                var inputCommand = TryGetValue(arguments, "<script_file_or_command_name>");
                var targetFile = TryGetValue(arguments, "<model_file>");
                var revitYear = TryGetValue(arguments, "--revit");

                // determine if script or command

                var modelFiles = new List<string>();
                // make sure file exists
                if (targetFile != null)
                    CommonUtils.VerifyFile(targetFile);

                if (inputCommand != null) {
                    // determine target revit year
                    int revitYearNumber = 0;
                    // if revit year is not specified try to get from model file
                    if (revitYear == null) {
                        if (targetFile != null) {
                            try {
                                revitYearNumber = new RevitModelFile(targetFile).RevitProduct.ProductYear;
                                // collect model names also
                                modelFiles.Add(targetFile);
                            }
                            catch (Exception ex) {
                                logger.Error(
                                    "Revit version must be explicitly specifies if using a model list file. | {0}",
                                    ex.Message
                                    );
                            }
                        }
                        // if no revit year and no model, run with latest revit
                        else
                            revitYearNumber = RevitProduct.ListInstalledProducts().Max(r => r.ProductYear);
                    }
                    // otherwise, grab the year from argument
                    else {
                        revitYearNumber = int.Parse(revitYear);
                        // prepare model list of provided
                        if (targetFile != null) {
                            try {
                                var modelVer = new RevitModelFile(targetFile).RevitProduct.ProductYear;
                                if (revitYearNumber < modelVer)
                                    logger.Warn("Model is newer than the target Revit version.");
                                else
                                    modelFiles.Add(targetFile);
                            }
                            catch {
                                // attempt at reading the list file and grab the model files only
                                foreach (var modelPath in File.ReadAllLines(targetFile)) {
                                    if (CommonUtils.VerifyFile(modelPath)) {
                                        try {
                                            var modelVer = new RevitModelFile(modelPath).RevitProduct.ProductYear;
                                            if (revitYearNumber < modelVer)
                                                logger.Warn("Model is newer than the target Revit version.");
                                            else
                                                modelFiles.Add(modelPath);
                                        }
                                        catch {
                                            logger.Error("File is not a valid Revit file: \"{0}\"", modelPath);
                                        }
                                    }
                                    else
                                        logger.Error("File does not exist: \"{0}\"", modelPath);
                                }
                            }
                        }
                    }

                    // now run
                    if (revitYearNumber != 0) {
                        // determine attached clone
                        var attachment = PyRevit.GetAttached(revitYearNumber);
                        if (attachment == null)
                            logger.Error("pyRevit is not attached to Revit \"{0}\". " +
                                         "Runner needs to use the attached clone and engine to execute the script.",
                                         revitYear);
                        else {
                            // determine script to run
                            string commandScriptPath = null;

                            if (!CommonUtils.VerifyPythonScript(inputCommand)) {
                                logger.Debug("Input is not a script file \"{0}\"", inputCommand);
                                logger.Debug("Attempting to find run command matching \"{0}\"", inputCommand);

                                // try to find run command in attached clone being used for execution
                                // if not found, try to get run command from all other installed extensions
                                var targetExtensions = new List<PyRevitExtension>();
                                targetExtensions.AddRange(attachment.Clone.GetExtensions());
                                targetExtensions.AddRange(PyRevit.GetInstalledExtensions());

                                foreach (PyRevitExtension ext in targetExtensions) {
                                    logger.Debug("Searching for run command in: \"{0}\"", ext.ToString());
                                    if (ext.Type == PyRevitExtensionTypes.RunnerExtension) {
                                        try {
                                            var cmdScript = ext.GetRunCommand(inputCommand);
                                            if (cmdScript != null) {
                                                logger.Debug("Run command matching \"{0}\" found: \"{1}\"",
                                                             inputCommand, cmdScript);
                                                commandScriptPath = cmdScript;
                                                break;
                                            }
                                        }
                                        catch {
                                            // does not include command
                                            continue;
                                        }
                                    }
                                }
                            }
                            else
                                commandScriptPath = inputCommand;

                            // if command is not found, stop
                            if (commandScriptPath == null)
                                throw new pyRevitException(
                                    string.Format("Run command not found: \"{0}\"", inputCommand)
                                    );

                            // RUN!
                            var execEnv = PyRevitRunner.Run(
                                attachment,
                                commandScriptPath,
                                modelFiles,
                                purgeTempFiles: arguments["--purge"].IsTrue
                                );

                            // print results (exec env)
                            PrintHeader("Execution Environment");
                            Console.WriteLine(string.Format("Execution Id: \"{0}\"", execEnv.ExecutionId));
                            Console.WriteLine(string.Format("Product: {0}", execEnv.Revit));
                            Console.WriteLine(string.Format("Clone: {0}", execEnv.Clone));
                            Console.WriteLine(string.Format("Engine: {0}", execEnv.Engine));
                            Console.WriteLine(string.Format("Script: \"{0}\"", execEnv.Script));
                            Console.WriteLine(string.Format("Working Directory: \"{0}\"", execEnv.WorkingDirectory));
                            Console.WriteLine(string.Format("Journal File: \"{0}\"", execEnv.JournalFile));
                            Console.WriteLine(string.Format("Manifest File: \"{0}\"", execEnv.PyRevitRunnerManifestFile));
                            Console.WriteLine(string.Format("Log File: \"{0}\"", execEnv.LogFile));
                            // report whether the env was purge or not
                            if (execEnv.Purged)
                                Console.WriteLine("Execution env is successfully purged.");

                            // print target models
                            if (execEnv.ModelPaths.Count() > 0) {
                                PrintHeader("Target Models");
                                foreach (var modelPath in execEnv.ModelPaths)
                                    Console.WriteLine(modelPath);
                            }

                            // print log file contents if exists
                            if (File.Exists(execEnv.LogFile)) {
                                PrintHeader("Execution Log");
                                Console.WriteLine(File.ReadAllText(execEnv.LogFile));
                            }
                        }
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit init --help
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "init") && arguments["--help"].IsTrue) {
                PrintSubHelpAndExit(new List<string>() { "init" },
                                    "Init pyRevit bundles");
            }


            // =======================================================================================================
            //  $ pyrevit init (ui | lib | run) <extension_name> [--usetemplate] [--templates=<templates_path>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "init", "ui")
                        || VerifyCommand(activeKeys, "init", "lib")
                        || VerifyCommand(activeKeys, "init", "run")) {

                PyRevitExtensionTypes extType = GetExtentionTypeFromArgument(arguments);

                var extDirPostfix = PyRevitExtension.GetExtensionDirExt(extType);

                var extensionName = TryGetValue(arguments, "<extension_name>");
                var templatesDir = TryGetValue(arguments, "--templates");
                if (extensionName != null) {
                    var pwd = Directory.GetCurrentDirectory();

                    if (CommonUtils.ConfirmFileNameIsUnique(pwd, extensionName)) {
                        var extDir = Path.Combine(
                            pwd,
                            string.Format("{0}{1}", extensionName, extDirPostfix)
                            );

                        var extTemplateDir = GetExtensionTemplate(extType, templatesDir: templatesDir);
                        if (arguments["--usetemplate"].IsTrue && extTemplateDir != null) {
                            CommonUtils.CopyDirectory(extTemplateDir, extDir);
                            Console.WriteLine(
                                string.Format("Extension directory created from template: \"{0}\"", extDir)
                                );
                        }
                        else {
                            if (!Directory.Exists(extDir)) {
                                var dinfo = Directory.CreateDirectory(extDir);
                                Console.WriteLine(string.Format("{0} directory created: \"{1}\"", extType, extDir));
                            }
                            else
                                throw new pyRevitException("Directory already exists.");
                        }

                    }
                    else
                        throw new pyRevitException(
                            string.Format("Another extension with name \"{0}\" already exists.", extensionName)
                            );
                }
            }

            // =======================================================================================================
            // $ pyrevit caches --help
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "caches") && arguments["--help"].IsTrue) {
                PrintSubHelpAndExit(new List<string>() { "caches" },
                                    "Manage pyRevit caches");
            }

            // =======================================================================================================
            // $ pyrevit init (tab | panel | panelopt | pull | split | splitpush | push | smart | command) <bundle_name> [--usetemplate] [--templates=<templates_path>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "init", "tab")
                        || VerifyCommand(activeKeys, "init", "panel")
                        || VerifyCommand(activeKeys, "init", "panelopt")
                        || VerifyCommand(activeKeys, "init", "pull")
                        || VerifyCommand(activeKeys, "init", "split")
                        || VerifyCommand(activeKeys, "init", "splitpush")
                        || VerifyCommand(activeKeys, "init", "push")
                        || VerifyCommand(activeKeys, "init", "smart")
                        || VerifyCommand(activeKeys, "init", "command")) {

                // determine bundle
                PyRevitBundleTypes bundleType = PyRevitBundleTypes.Unknown;

                if (arguments["tab"].IsTrue)
                    bundleType = PyRevitBundleTypes.Tab;
                else if (arguments["panel"].IsTrue)
                    bundleType = PyRevitBundleTypes.Panel;
                else if (arguments["panelopt"].IsTrue)
                    bundleType = PyRevitBundleTypes.PanelButton;
                else if (arguments["pull"].IsTrue)
                    bundleType = PyRevitBundleTypes.PullDown;
                else if (arguments["split"].IsTrue)
                    bundleType = PyRevitBundleTypes.SplitButton;
                else if (arguments["splitpush"].IsTrue)
                    bundleType = PyRevitBundleTypes.SplitPushButton;
                else if (arguments["push"].IsTrue)
                    bundleType = PyRevitBundleTypes.PushButton;
                else if (arguments["smart"].IsTrue)
                    bundleType = PyRevitBundleTypes.SmartButton;
                else if (arguments["command"].IsTrue)
                    bundleType = PyRevitBundleTypes.NoButton;

                if (bundleType != PyRevitBundleTypes.Unknown) {
                    var bundleName = TryGetValue(arguments, "<bundle_name>");
                    var templatesDir = TryGetValue(arguments, "--templates");
                    if (bundleName != null) {
                        var pwd = Directory.GetCurrentDirectory();

                        if (CommonUtils.ConfirmFileNameIsUnique(pwd, bundleName)) {
                            var bundleDir = Path.Combine(
                                pwd,
                                string.Format("{0}{1}", bundleName, PyRevitBundle.GetBundleDirExt(bundleType))
                                );

                            var bundleTempDir = GetBundleTemplate(bundleType, templatesDir: templatesDir);
                            if (arguments["--usetemplate"].IsTrue && bundleTempDir != null) {
                                CommonUtils.CopyDirectory(bundleTempDir, bundleDir);
                                Console.WriteLine(
                                    string.Format("Bundle directory created from template: \"{0}\"", bundleDir)
                                    );
                            }
                            else {
                                if (!Directory.Exists(bundleDir)) {
                                    var dinfo = Directory.CreateDirectory(bundleDir);
                                    Console.WriteLine(string.Format("Bundle directory created: \"{0}\"", bundleDir));
                                }
                                else
                                    throw new pyRevitException("Directory already exists.");
                            }

                        }
                        else
                            throw new pyRevitException(
                                string.Format("Another bundle with name \"{0}\" already exists.", bundleName)
                                );
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit init templates
            // $ pyrevit init templates (add | forget) <init_templates_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "init", "templates")) {
                Console.WriteLine(Directory.GetCurrentDirectory());
            }

            // =======================================================================================================
            // $ pyrevit caches clear (--all | <revit_year>)
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "caches", "clear")) {
                if (arguments["--all"].IsTrue)
                    PyRevit.ClearAllCaches();
                else if (arguments["<revit_year>"] != null) {
                    var revitYear = TryGetValue(arguments, "<revit_year>");
                    if (revitYear != null)
                        PyRevit.ClearCache(int.Parse(revitYear));
                }
            }

            // =======================================================================================================
            // $ pyrevit config --help
            // $ pyrevit config <template_config_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "config")) {

                if (arguments["--help"].IsTrue)
                    PrintSubHelpAndExit(new List<string>() { "config" },
                                        "Configure pyRevit for current user");

                var templateConfigFilePath = TryGetValue(arguments, "<template_config_path>");
                if (templateConfigFilePath != null)
                    PyRevit.SeedConfig(setupFromTemplate: templateConfigFilePath);
            }

            // =======================================================================================================
            // $ pyrevit configs
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs") && arguments["--help"].IsTrue) {
                PrintSubHelpAndExit(new List<string>() { "configs" },
                                    "Manage pyRevit configurations");
            }

            // =======================================================================================================
            // $ pyrevit configs logs [(none | verbose | debug)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "logs"))
                Console.WriteLine(string.Format("Logging Level is {0}", PyRevit.GetLoggingLevel().ToString()));

            else if (VerifyCommand(activeKeys, "configs", "logs", "none"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.None);

            else if (VerifyCommand(activeKeys, "configs", "logs", "verbose"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.Verbose);

            else if (VerifyCommand(activeKeys, "configs", "logs", "debug"))
                PyRevit.SetLoggingLevel(PyRevitLogLevels.Debug);

            // =======================================================================================================
            // $ pyrevit configs allowremotedll [(enable | disable)]
            // =======================================================================================================
            // TODO: Implement allowremotedll
            else if (VerifyCommand(activeKeys, "configs", "allowremotedll"))
                logger.Error("Not Yet Implemented");

            // =======================================================================================================
            // $ pyrevit configs checkupdates [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "checkupdates"))
                Console.WriteLine(
                    string.Format("Check Updates is {0}",
                    PyRevit.GetCheckUpdates() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "checkupdates", "enable")
                    || VerifyCommand(activeKeys, "configs", "checkupdates", "disable"))
                PyRevit.SetCheckUpdates(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs autoupdate [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "autoupdate"))
                Console.WriteLine(
                    string.Format("Auto Update is {0}",
                    PyRevit.GetAutoUpdate() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "autoupdate", "enable")
                    || VerifyCommand(activeKeys, "configs", "autoupdate", "disable"))
                PyRevit.SetAutoUpdate(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs rocketmode [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "rocketmode"))
                Console.WriteLine(
                    string.Format("Rocket Mode is {0}",
                    PyRevit.GetRocketMode() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "rocketmode", "enable")
                    || VerifyCommand(activeKeys, "configs", "rocketmode", "disable"))
                PyRevit.SetRocketMode(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs filelogging [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "filelogging"))
                Console.WriteLine(
                    string.Format("File Logging is {0}",
                    PyRevit.GetFileLogging() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "filelogging", "enable")
                    || VerifyCommand(activeKeys, "configs", "filelogging", "disable"))
                PyRevit.SetFileLogging(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs loadbeta [(enable | disable)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "loadbeta"))
                Console.WriteLine(
                    string.Format("Load Beta is {0}",
                    PyRevit.GetLoadBetaTools() ? "Enabled" : "Disabled")
                    );

            else if (VerifyCommand(activeKeys, "configs", "loadbeta", "enable")
                    || VerifyCommand(activeKeys, "configs", "loadbeta", "disable"))
                PyRevit.SetLoadBetaTools(arguments["enable"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs usercanupdate [(Yes | No)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usercanupdate"))
                Console.WriteLine(
                    string.Format("User {0} update.",
                    PyRevit.GetUserCanUpdate() ? "CAN" : "CAN NOT")
                    );

            else if (VerifyCommand(activeKeys, "configs", "usercanupdate", "Yes")
                    || VerifyCommand(activeKeys, "configs", "usercanupdate", "No"))
                PyRevit.SetUserCanUpdate(arguments["Yes"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs usercanextend [(Yes | No)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usercanextend"))
                Console.WriteLine(
                    string.Format("User {0} extend.",
                    PyRevit.GetUserCanExtend() ? "CAN" : "CAN NOT")
                    );

            else if (VerifyCommand(activeKeys, "configs", "usercanextend", "Yes")
                    || VerifyCommand(activeKeys, "configs", "usercanextend", "No"))
                PyRevit.SetUserCanExtend(arguments["Yes"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs usercanconfig [(Yes | No)]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usercanconfig"))
                Console.WriteLine(
                    string.Format("User {0} config.",
                    PyRevit.GetUserCanConfig() ? "CAN" : "CAN NOT")
                    );

            else if (VerifyCommand(activeKeys, "configs", "usercanconfig", "Yes")
                    || VerifyCommand(activeKeys, "configs", "usercanconfig", "No"))
                PyRevit.SetUserCanConfig(arguments["Yes"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs usagelogging
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging")) {
                Console.WriteLine(
                    string.Format("Usage logging is {0}",
                    PyRevit.GetUsageReporting() ? "Enabled" : "Disabled")
                    );
                Console.WriteLine(string.Format("Log File Path: {0}", PyRevit.GetUsageLogFilePath()));
                Console.WriteLine(string.Format("Log Server Url: {0}", PyRevit.GetUsageLogServerUrl()));
            }

            // =======================================================================================================
            // $ pyrevit configs usagelogging enable (file | server) <dest_path>
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "enable", "file"))
                PyRevit.EnableUsageReporting(logFilePath: TryGetValue(arguments, "<dest_path>"));

            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "enable", "server"))
                PyRevit.EnableUsageReporting(logServerUrl: TryGetValue(arguments, "<dest_path>"));

            // =======================================================================================================
            // $ pyrevit configs usagelogging disable
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "usagelogging", "disable"))
                PyRevit.DisableUsageReporting();

            // =======================================================================================================
            // $ pyrevit configs outputcss [<css_path>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "outputcss")) {
                if (arguments["<css_path>"] == null)
                    Console.WriteLine(
                        string.Format("Output Style Sheet is set to: {0}",
                        PyRevit.GetOutputStyleSheet()
                        ));
                else
                    PyRevit.SetOutputStyleSheet(TryGetValue(arguments, "<css_path>"));
            }

            // =======================================================================================================
            // $ pyrevit configs seed [--lock]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs", "seed"))
                PyRevit.SeedConfig(makeCurrentUserAsOwner: arguments["--lock"].IsTrue);

            // =======================================================================================================
            // $ pyrevit configs <option_path> [(enable | disable)]
            // $ pyrevit configs <option_path> [<option_value>]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "configs")) {
                if (arguments["<option_path>"] != null) {
                    // extract section and option names
                    string orignalOptionValue = TryGetValue(arguments, "<option_path>");
                    if (orignalOptionValue.Split(':').Count() == 2) {
                        string configSection = orignalOptionValue.Split(':')[0];
                        string configOption = orignalOptionValue.Split(':')[1];

                        // if no value provided, read the value
                        if (arguments["<option_value>"] != null)
                            PyRevit.SetConfig(
                                configSection,
                                configOption,
                                TryGetValue(arguments, "<option_value>")
                                );
                        else if (arguments["<option_value>"] == null)
                            Console.WriteLine(
                                string.Format("{0} = {1}",
                                configOption,
                                PyRevit.GetConfig(configSection, configOption)
                                ));
                    }
                }
            }

            else if (VerifyCommand(activeKeys, "configs", "enable")
                    || VerifyCommand(activeKeys, "configs", "disable")) {
                if (arguments["<option_path>"] != null) {
                    // extract section and option names
                    string orignalOptionValue = TryGetValue(arguments, "<option_path>");
                    if (orignalOptionValue.Split(':').Count() == 2) {
                        string configSection = orignalOptionValue.Split(':')[0];
                        string configOption = orignalOptionValue.Split(':')[1];

                        PyRevit.SetConfig(configSection, configOption, arguments["enable"].IsTrue);
                    }
                }
            }

            // =======================================================================================================
            // $ pyrevit cli --help
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "cli") && arguments["--help"].IsTrue) {
                PrintSubHelpAndExit(new List<string>() { "cli" },
                                    "Manage this utility");
            }
            // =======================================================================================================
            // $ pyrevit cli addshortcut <shortcut_name> <shortcut_args> [--desc=<shortcut_description>] [--allusers]
            // =======================================================================================================
            else if (VerifyCommand(activeKeys, "cli", "addshortcut")) {
                var shortcutName = TryGetValue(arguments, "<shortcut_name>");
                var shortcutArgs = TryGetValue(arguments, "<shortcut_args>");
                var shortcutDesc = TryGetValue(arguments, "--desc");
                if (shortcutName != null && shortcutArgs != null) {
                    var processPath = GetProcessPath();
                    var iconPath = Path.Combine(processPath, "pyRevit.ico");
                    CommonUtils.AddShortcut(
                        shortcutName,
                        "pyRevit",
                        GetProcessFileName(),
                        shortcutArgs,
                        processPath,
                        iconPath,
                        shortcutDesc,
                        allUsers: arguments["--allusers"].IsTrue
                        );
                }
            }

            // =======================================================================================================
            // $ pyrevit (-h|--help)
            // =======================================================================================================
            else if (arguments["--help"].IsTrue || arguments["-h"].IsTrue) {
                Console.WriteLine(PrettyHelp);
            }
        }

        // get enabled keywords
        private static List<string> ExtractEnabledKeywords(IDictionary<string, ValueObject> arguments) {
            // grab active keywords
            var enabledKeywords = new List<string>();
            foreach (var argument in arguments.OrderBy(x => x.Key)) {
                if (argument.Value != null
                        && !argument.Key.Contains("<")
                        && !argument.Key.Contains(">")
                        && !argument.Key.Contains("--")
                        && argument.Value.IsTrue) {
                    logger.Debug("Active Keyword: {0}", argument.Key);
                    enabledKeywords.Add(argument.Key);
                }
            }
            return enabledKeywords;
        }

        // verify cli command based on keywords that must be true and the rest of keywords must be false
        private static bool VerifyCommand(
                IEnumerable<string> enabledKeywords, params string[] keywords) {
            // check all given keywords are active
            if (keywords.Length != enabledKeywords.Count())
                return false;

            foreach (var keyword in keywords)
                if (!enabledKeywords.Contains(keyword))
                    return false;

            return true;
        }

        // safely try to get a value from arguments dictionary, return null on errors
        private static string TryGetValue(
                IDictionary<string, ValueObject> arguments, string key, string defaultValue = null) {
            return arguments[key] != null ? arguments[key].Value as string : defaultValue;
        }

        // process generated error codes and show prompts if necessary
        private static void ProcessErrorCodes() {
        }

        // process generated error codes and show prompts if necessary
        private static void LogException(Exception ex, pyRevitManagerLogLevel logLevel) {
            if (logLevel == pyRevitManagerLogLevel.Debug)
                logger.Error(string.Format("{0} ({1})\n{2}", ex.Message, ex.GetType().ToString(), ex.StackTrace));
            else
                logger.Error(string.Format("{0}\nRun with \"--debug\" option to see debug messages", ex.Message));
        }

        // print info on a revit model
        private static void PrintModelInfo(RevitModelFile model) {
            Console.WriteLine(string.Format("Created in: {0} ({1}({2}))",
                                model.RevitProduct.ProductName,
                                model.RevitProduct.BuildNumber,
                                model.RevitProduct.BuildTarget));
            Console.WriteLine(string.Format("Workshared: {0}", model.IsWorkshared ? "Yes" : "No"));
            if (model.IsWorkshared)
                Console.WriteLine(string.Format("Central Model Path: {0}", model.CentralModelPath));
            Console.WriteLine(string.Format("Last Saved Path: {0}", model.LastSavedPath));
            Console.WriteLine(string.Format("Document Id: {0}", model.UniqueId));
            Console.WriteLine(string.Format("Open Workset Settings: {0}", model.OpenWorksetConfig));
            Console.WriteLine(string.Format("Document Increment: {0}", model.DocumentIncrement));

            if (model.IsFamily) {
                Console.WriteLine("Model is a Revit Family!");
                Console.WriteLine(string.Format("Category Name: {0}", model.CategoryName));
                Console.WriteLine(string.Format("Host Category Name: {0}", model.HostCategoryName));
            }
        }

        // export model info to csv
        private static void ExportModelInfoToCSV(IEnumerable<RevitModelFile> models,
                                                 string outputCSV,
                                                 List<(string, string)> errorList = null) {
            logger.Info(string.Format("Building CSV data to \"{0}\"", outputCSV));
            var csv = new StringBuilder();
            csv.Append(
                "filepath,productname,buildnumber,isworkshared,centralmodelpath,lastsavedpath,uniqueid,error\n"
                );
            foreach (var model in models) {
                var data = new List<string>() {
                    string.Format("\"{0}\"", model.FilePath),
                    string.Format("\"{0}\"", model.RevitProduct != null ? model.RevitProduct.ProductName : ""),
                    string.Format("\"{0}\"", model.RevitProduct != null ? model.RevitProduct.BuildNumber : ""),
                    string.Format("\"{0}\"", model.IsWorkshared ? "True" : "False"),
                    string.Format("\"{0}\"", model.CentralModelPath),
                    string.Format("\"{0}\"", model.LastSavedPath),
                    string.Format("\"{0}\"", model.UniqueId.ToString()),
                    ""
                };

                csv.Append(string.Join(",", data) + "\n");
            }

            // write list of files with errors
            logger.Debug("Adding errors to \"{0}\"", outputCSV);
            foreach (var errinfo in errorList)
                csv.Append(string.Format("\"{0}\",,,,,,,\"{1}\"\n", errinfo.Item1, errinfo.Item2));

            logger.Info(string.Format("Writing results to \"{0}\"", outputCSV));
            File.WriteAllText(outputCSV, csv.ToString());
        }

        private static void ReportCloneAsNoGit(PyRevitClone clone) {
            Console.WriteLine(
                string.Format("Clone \"{0}\" is a deployment and is not a git repo.",
                clone.Name)
                );
        }

        private static string GetProcessFileName() {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static string GetProcessPath() {
            return Path.GetDirectoryName(GetProcessFileName());
        }

        private static bool IsRunningInsideClone(PyRevitClone clone) {
            return GetProcessPath().NormalizeAsPath().Contains(clone.ClonePath.NormalizeAsPath());
        }

        private static void UpdateFromOutsideAndClose(PyRevitClone clone, bool showgui = false) {
            var userTemp = System.Environment.ExpandEnvironmentVariables("%TEMP%");
            var sourceUpdater = Path.Combine(GetProcessPath(), updaterExecutive);
            var updaterPath = Path.Combine(userTemp, updaterExecutive);

            // prepare outside updater
            logger.Debug("Setting up \"{0}\" to \"{1}\"", sourceUpdater, updaterPath);
            File.Copy(sourceUpdater, updaterPath, overwrite: true);

            // make a updater bat file
            var updaterBATFile = Path.Combine(userTemp, updaterExecutive.ToLower().Replace(".exe", ".bat"));
            using (var batFile = new StreamWriter(File.Create(updaterBATFile))) {
                batFile.WriteLine("@ECHO OFF");
                batFile.WriteLine("TIMEOUT /t 1 /nobreak >NUL  2>NUL");
                batFile.WriteLine("TASKKILL /IM \"{0}\" >NUL  2>NUL", Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));
                if (showgui)
                    batFile.WriteLine("START \"\" /B \"{0}\" \"{1}\" --gui", updaterPath, clone.ClonePath);
                else
                    batFile.WriteLine("START \"\" /B \"{0}\" \"{1}\"", updaterPath, clone.ClonePath);
            }

            // launch update
            ProcessStartInfo updaterProcessInfo = new ProcessStartInfo(updaterBATFile);
            updaterProcessInfo.WorkingDirectory = Path.GetDirectoryName(updaterPath);
            updaterProcessInfo.UseShellExecute = false;
            updaterProcessInfo.CreateNoWindow = true;
            logger.Debug("Calling outside update and exiting...");
            Process.Start(updaterProcessInfo);
            // and exit self
            Environment.Exit(0);
        }

        // extensions and bundles
        private static string GetExtensionTemplate(PyRevitExtensionTypes extType, string templatesDir = null) {
            templatesDir = templatesDir != null ? templatesDir : Path.Combine(GetProcessPath(), "templates");
            if (CommonUtils.VerifyPath(templatesDir)) {
                var extTempPath =
                    Path.Combine(templatesDir, "template" + PyRevitExtension.GetExtensionDirExt(extType));
                if (CommonUtils.VerifyPath(extTempPath))
                    return extTempPath;
            }
            else
                throw new pyRevitException(
                    string.Format("Templates directory does not exist at \"{0}\"", templatesDir)
                    );


            return null;
        }

        private static string GetBundleTemplate(PyRevitBundleTypes bundleType, string templatesDir = null) {
            templatesDir = templatesDir != null ? templatesDir : Path.Combine(GetProcessPath(), "templates");
            if (CommonUtils.VerifyPath(templatesDir)) {
                var bundleTempPath =
                    Path.Combine(templatesDir, "template" + PyRevitBundle.GetBundleDirExt(bundleType));
                if (CommonUtils.VerifyPath(bundleTempPath))
                    return bundleTempPath;
            }
            else
                throw new pyRevitException(
                    string.Format("Templates directory does not exist at \"{0}\"", templatesDir)
                    );

            return null;
        }

        private static PyRevitExtensionTypes GetExtentionTypeFromArgument(IDictionary<string, ValueObject> arguments) {
            PyRevitExtensionTypes extType = PyRevitExtensionTypes.Unknown;
            if (arguments["ui"].IsTrue)
                extType = PyRevitExtensionTypes.UIExtension;
            else if (arguments["lib"].IsTrue)
                extType = PyRevitExtensionTypes.LibraryExtension;
            else if (arguments["run"].IsTrue)
                extType = PyRevitExtensionTypes.RunnerExtension;

            return extType;
        }

        // print functions
        private static void PrintHeader(string header) {
            Console.WriteLine(string.Format("==> {0}", header), Color.Green);
        }

        private static void PrintSubHelpAndExit(IEnumerable<string> docoptKeywords,
                                                string title,
                                                IDictionary<string, string> commands = null,
                                                IDictionary<string, string> options = null) {
            // build a help guide for a subcommand based on doctop usage entries
            Console.WriteLine(title + Environment.NewLine);
            foreach (var hline in doctopUsagePatterns.GetLines())
                if (hline.Contains("Usage:"))
                    Console.WriteLine(hline);
                else
                    foreach (var kword in docoptKeywords) {
                        if ((hline.Contains("pyrevit " + kword + " ") || hline.EndsWith(" " + kword))
                            && !hline.Contains("pyrevit " + kword + " --help"))
                            Console.WriteLine(hline);
                    }

            // print commands help
            Console.WriteLine();
            if (commands != null) {
                Console.WriteLine("    Commands:");
                foreach (var commandPair in commands) {
                    Console.WriteLine(
                        string.Format("        {0,-20}{1}", commandPair.Key, commandPair.Value)
                        );
                }
                Console.WriteLine();
            }

            // print options help
            if (options != null) {
                Console.WriteLine("    Options:");
                foreach(var optionPair in options) {
                    Console.WriteLine(
                        string.Format("        {0,-20}{1}", optionPair.Key, optionPair.Value)
                        );
                }

                Console.WriteLine();
            }

            Environment.Exit(0);
        }

        private static void PrintClones() {
            PrintHeader("Registered Clones (full git repos)");
            var clones = PyRevit.GetRegisteredClones().OrderBy(x => x.Name);
            foreach (var clone in clones.Where(c => c.IsRepoDeploy))
                Console.WriteLine(clone);

            PrintHeader("Registered Clones (deployed from archive)");
            foreach (var clone in clones.Where(c => !c.IsRepoDeploy))
                Console.WriteLine(clone);
        }

        private static void PrintAttachments(int revitYear = 0) {
            PrintHeader("Attachments");
            foreach (var attachment in PyRevit.GetAttachments().OrderByDescending(x => x.Product.Version)) {
                if (attachment.Clone != null && attachment.Engine != null) {
                    if (revitYear == 0)
                        Console.WriteLine(attachment);
                    else if (revitYear == attachment.Product.ProductYear)
                        Console.WriteLine(attachment);
                }
                else
                    logger.Error(
                        string.Format("pyRevit is attached to Revit {0} but can not determine the clone and engine",
                                      attachment.Product.ProductYear)
                        );
            }
        }

        private static void PrintExtensions(IEnumerable<PyRevitExtension> extList = null,
                                            string headerPrefix = "Installed") {
            if (extList == null)
                extList = PyRevit.GetInstalledExtensions();

            PrintHeader(string.Format("{0} Extensions", headerPrefix));
            foreach (PyRevitExtension ext in extList.OrderBy(x => x.Name))
                Console.WriteLine(ext);
        }

        private static void PrintExtensionDefinitions(IEnumerable<PyRevitExtensionDefinition> extList,
                                                     string headerPrefix = "Registered") {
            PrintHeader(string.Format("{0} Extensions", headerPrefix));
            foreach (PyRevitExtensionDefinition ext in extList)
                Console.WriteLine(ext);
        }

        private static void PrintExtensionSearchPaths() {
            PrintHeader("Default Extension Search Path");
            Console.WriteLine(PyRevit.pyRevitDefaultExtensionsPath);
            PrintHeader("Extension Search Paths");
            foreach (var searchPath in PyRevit.GetRegisteredExtensionSearchPaths())
                Console.WriteLine(searchPath);
        }

        private static void PrintExtensionLookupSources() {
            PrintHeader("Extension Sources - Default");
            Console.WriteLine(PyRevit.GetDefaultExtensionLookupSource());
            PrintHeader("Extension Sources - Additional");
            foreach (var extLookupSrc in PyRevit.GetRegisteredExtensionLookupSources())
                Console.WriteLine(extLookupSrc);
        }

        private static void PrintInstalledRevits() {
            PrintHeader("Installed Revits");
            foreach (var revit in RevitProduct.ListInstalledProducts().OrderByDescending(x => x.Version))
                Console.WriteLine(revit);
        }

        private static void PrintRunningRevits() {
            PrintHeader("Running Revit Instances");
            foreach (var revit in RevitController.ListRunningRevits().OrderByDescending(x => x.RevitProduct.Version))
                Console.WriteLine(revit);
        }

        private static void PrintPyRevitPaths() {
            PrintHeader("Cache Directory");
            Console.WriteLine(string.Format("\"{0}\"", PyRevit.pyRevitAppDataPath));
        }

        private static void PrinUserEnv() {
            PrintHeader("User Environment");
            Console.WriteLine(UserEnv.GetWindowsVersion());
            Console.WriteLine(string.Format("Executing User: {0}\\{1}",
                                            Environment.UserDomainName, Environment.UserName));
            Console.WriteLine(string.Format("Active User: {0}", UserEnv.GetLoggedInUserName()));
            Console.WriteLine(string.Format("Adming Access: {0}", UserEnv.IsRunAsAdmin() ? "Yes" : "No"));
            Console.WriteLine(string.Format("%APPDATA%: \"{0}\"",
                                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
            Console.WriteLine(string.Format("Latest Installed .Net Framework: \"{0}\"",
                                            UserEnv.GetInstalledDotNetVersion()));
            try {
                string targetPacks = "";
                foreach (string targetPackagePath in UserEnv.GetInstalledDotnetTargetPacks())
                    targetPacks += string.Format("{0} ", Path.GetFileName(targetPackagePath));
                Console.WriteLine(string.Format("Installed .Net Target Packs: {0}", targetPacks));
            }
            catch {
                Console.WriteLine("No .Net Target Packs are installed.");
            }

            try {
                string targetPacks = "";
                foreach (string targetPackagePath in UserEnv.GetInstalledDotnetCoreTargetPacks())
                    targetPacks += string.Format("v{0} ", Path.GetFileName(targetPackagePath));
                Console.WriteLine(string.Format("Installed .Net-Core Target Packs: {0}", targetPacks));
            }
            catch {
                Console.WriteLine("No .Ne-Core Target Packs are installed.");
            }

            Console.WriteLine(string.Format("pyRevit CLI {0}", CLIVersion.ToString()));
        }
    }
}
