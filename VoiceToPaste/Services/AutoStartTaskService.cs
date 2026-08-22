using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;
using Serilog;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    public enum AutoStartTaskStatus
    {
        Missing,
        Configured,
        Outdated,
    }

    /// <summary>
    /// Zarządza jednym zadaniem Harmonogramu uruchamiającym VoiceToPaste po zalogowaniu
    /// bieżącego użytkownika. Stała nazwa ogranicza operacje wyłącznie do zadania aplikacji.
    /// </summary>
    public sealed class AutoStartTaskService
    {
        private static readonly ILogger Logger = Log.ForContext<AutoStartTaskService>();
        private static readonly TimeSpan LogonDelay = TimeSpan.FromSeconds(15);
        private const string TaskName = "VoiceToPaste.Autostart";

        private readonly string _executablePath;
        private readonly string _workingDirectory;

        public AutoStartTaskService()
            : this(
                Environment.ProcessPath
                    ?? throw LocalizedExceptionFactory.InvalidOperation("ApplicationExecutablePathUnavailable"),
                ApplicationPaths.Directory)
        {
        }

        internal AutoStartTaskService(string executablePath, string workingDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

            _executablePath = Path.GetFullPath(executablePath);
            _workingDirectory = Path.GetFullPath(workingDirectory);
        }

        /// <summary>
        /// Tworzy zadanie lub zastępuje jego wcześniejszą definicję aktualną ścieżką aplikacji.
        /// Zadanie działa tylko w interaktywnej sesji bieżącego użytkownika i z najwyższymi uprawnieniami.
        /// </summary>
        public void Enable()
        {
            try
            {
                using var taskService = new TaskService();
                var userSid = GetCurrentUserSid();
                var definition = taskService.NewTask();

                definition.RegistrationInfo.Description = UiStrings.Get("AutoStartTaskDescription");
                definition.Principal.UserId = userSid;
                definition.Principal.LogonType = TaskLogonType.InteractiveToken;
                definition.Principal.RunLevel = TaskRunLevel.Highest;

                definition.Triggers.Add(new LogonTrigger
                {
                    UserId = userSid,
                    // Explorer potrzebuje chwili na utworzenie obszaru powiadomień po zalogowaniu.
                    Delay = LogonDelay,
                });
                definition.Actions.Add(new ExecAction(_executablePath, arguments: null, _workingDirectory));

                // Autostart ma działać również na laptopie i nie może uruchamiać drugiej instancji.
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;
                definition.Settings.StartWhenAvailable = true;
                definition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                taskService.RootFolder.RegisterTaskDefinition(
                    TaskName,
                    definition,
                    TaskCreation.CreateOrUpdate,
                    userSid,
                    password: null,
                    TaskLogonType.InteractiveToken);

                Logger.Information("Created or updated autostart task {TaskName} for user {UserSid}.",
                    TaskName,
                    userSid);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create or update autostart task {TaskName}.", TaskName);
                throw LocalizedExceptionFactory.InvalidOperation("AutoStartConfigurationFailed", ex);
            }
        }

        /// <summary>
        /// Rozróżnia brak zadania, poprawną konfigurację i definicję wymagającą naprawy.
        /// Sprawdzenie nie modyfikuje Harmonogramu.
        /// </summary>
        public AutoStartTaskStatus GetStatus()
        {
            try
            {
                using var taskService = new TaskService();
                var task = taskService.GetTask(TaskName);
                if (task == null)
                    return AutoStartTaskStatus.Missing;

                var status = HasExpectedDefinition(task)
                    ? AutoStartTaskStatus.Configured
                    : AutoStartTaskStatus.Outdated;

                Logger.Debug("Autostart task {TaskName} status: {TaskStatus}.", TaskName, status);
                return status;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to check autostart task {TaskName}.", TaskName);
                throw LocalizedExceptionFactory.InvalidOperation("AutoStartStatusCheckFailed", ex);
            }
        }

        /// <summary>
        /// Usuwa wyłącznie zadanie o stałej nazwie należącej do VoiceToPaste.
        /// Brak zadania jest traktowany jako poprawnie wyłączony autostart.
        /// </summary>
        public void Disable()
        {
            try
            {
                using var taskService = new TaskService();
                taskService.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
                Logger.Information("Deleted autostart task {TaskName}.", TaskName);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to delete autostart task {TaskName}.", TaskName);
                throw LocalizedExceptionFactory.InvalidOperation("AutoStartDisableFailed", ex);
            }
        }

        private bool HasExpectedDefinition(Microsoft.Win32.TaskScheduler.Task task)
        {
            var definition = task.Definition;
            var userSid = GetCurrentUserSid();

            if (!task.Enabled ||
                definition.Principal.RunLevel != TaskRunLevel.Highest ||
                definition.Principal.LogonType != TaskLogonType.InteractiveToken ||
                !RepresentsUser(definition.Principal.UserId, userSid) ||
                definition.Settings.DisallowStartIfOnBatteries ||
                definition.Settings.StopIfGoingOnBatteries ||
                !definition.Settings.StartWhenAvailable ||
                definition.Settings.MultipleInstances != TaskInstancesPolicy.IgnoreNew ||
                definition.Settings.ExecutionTimeLimit != TimeSpan.Zero)
            {
                return false;
            }

            if (definition.Triggers.Count != 1 ||
                definition.Triggers[0] is not LogonTrigger logonTrigger ||
                !logonTrigger.Enabled ||
                logonTrigger.Delay != LogonDelay ||
                !RepresentsUser(logonTrigger.UserId, userSid))
            {
                return false;
            }

            if (definition.Actions.Count != 1 || definition.Actions[0] is not ExecAction action)
                return false;

            return string.IsNullOrEmpty(action.Arguments) &&
                PathsEqual(action.Path, _executablePath) &&
                PathsEqual(action.WorkingDirectory, _workingDirectory);
        }

        private static string GetCurrentUserSid()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value
                ?? throw LocalizedExceptionFactory.InvalidOperation("CurrentUserIdentifierUnavailable");
        }

        private static bool RepresentsUser(string? userId, string expectedSid)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            try
            {
                var identity = userId.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase)
                    ? new SecurityIdentifier(userId)
                    : (SecurityIdentifier)new NTAccount(userId).Translate(typeof(SecurityIdentifier));

                return string.Equals(identity.Value, expectedSid, StringComparison.OrdinalIgnoreCase);
            }
            catch (IdentityNotMappedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool PathsEqual(string? firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath),
                    Path.GetFullPath(secondPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }
    }
}
