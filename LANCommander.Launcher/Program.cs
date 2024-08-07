﻿using Emzi0767.NtfsDataStreams;
using LANCommander.Launcher.Data;
using LANCommander.Launcher.Extensions;
using LANCommander.Launcher.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web;

namespace LANCommander.Launcher
{
    class Program
    {
        static Logger Logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        static void Main(string[] args)
        {
            Logger?.Debug("Starting up launcher...");
            Logger?.Debug("Loading settings from file");
            var settings = SettingService.GetSettings();


            var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("app");

            builder.Services.AddLogging();
            builder.Services.AddCustomWindow();
            builder.Services.AddAntDesign();
            builder.Services.AddDbContext<DbContext, DatabaseContext>();

            #region Configure Logging
            Logger?.Debug("Configuring logging...");

            builder.Services.AddLogging(loggingBuilder =>
            {
                var loggerConfig = new LoggingConfiguration();

                NLog.GlobalDiagnosticsContext.Set("StoragePath", settings.Debug.LoggingPath);
                NLog.GlobalDiagnosticsContext.Set("ArchiveEvery", settings.Debug.LoggingArchivePeriod);
                NLog.GlobalDiagnosticsContext.Set("MaxArchiveFiles", settings.Debug.MaxArchiveFiles);
                NLog.GlobalDiagnosticsContext.Set("LoggingLevel", settings.Debug.LoggingLevel);

                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(settings.Debug.LoggingLevel);
                loggingBuilder.AddNLog();
            });
            #endregion

            #region Register Client
            Logger?.Debug("Registering LANCommander client...");
            var client = new SDK.Client(settings.Authentication.ServerAddress, settings.Games.DefaultInstallDirectory);

            client.UseToken(new SDK.Models.AuthToken
            {
                AccessToken = settings.Authentication.AccessToken,
                RefreshToken = settings.Authentication.RefreshToken,
            });

            builder.Services.AddSingleton(client);
            builder.Services.AddSingleton<MessageBusService>();
            #endregion

            #region Register Services
            Logger?.Debug("Registering services...");

            builder.Services.AddScoped<CollectionService>();
            builder.Services.AddScoped<CompanyService>();
            builder.Services.AddScoped<EngineService>();
            builder.Services.AddScoped<GameService>();
            builder.Services.AddScoped<GenreService>();
            builder.Services.AddScoped<PlatformService>();
            builder.Services.AddScoped<MultiplayerModeService>();
            builder.Services.AddScoped<TagService>();
            builder.Services.AddScoped<MediaService>();
            builder.Services.AddScoped<ProfileService>();
            builder.Services.AddScoped<PlaySessionService>();
            builder.Services.AddScoped<RedistributableService>();
            builder.Services.AddScoped<ScriptService>();
            builder.Services.AddScoped<SaveService>();
            builder.Services.AddScoped<ImportService>();
            builder.Services.AddScoped<LibraryService>();
            builder.Services.AddScoped<DownloadService>();
            builder.Services.AddScoped<UpdateService>();
            #endregion

            #region Build Application
            Logger?.Debug("Building application...");

            var app = builder.Build();

            app.MainWindow
                .SetTitle("LANCommander")
                .SetUseOsDefaultLocation(true)
                .SetChromeless(true)
                .SetResizable(true)
                .RegisterCustomSchemeHandler("media", (object sender, string scheme, string url, out string contentType) =>
                {
                    var uri = new Uri(url);
                    var query = HttpUtility.ParseQueryString(uri.Query);

                    var filePath = Path.Combine(MediaService.GetStoragePath(), uri.Host);

                    contentType = query["mime"];

                    if (File.Exists(filePath))
                        return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    else
                        return null;
                })
                .RegisterWebMessageReceivedHandler(async (object sender, string message) =>
                {
                    switch (message)
                    {
                        case "import":
                            using (var scope = app.Services.CreateScope())
                            {
                                var importService = scope.ServiceProvider.GetService<ImportService>();

                                var window = (PhotinoWindow)sender;

                                await importService.ImportAsync();

                                window.SendWebMessage("importComplete");
                            }
                            break;
                    }
                });
            #endregion

            AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };

            #region Scaffold Required Directories
            try
            {
                Logger?.Debug("Scaffolding required directories...");

                string[] requiredDirectories = new string[]
                {
	                settings.Debug.LoggingPath,
	                settings.Media.StoragePath,
	                settings.Games.DefaultInstallDirectory,
	                settings.Database.BackupsPath,
	                settings.Updates.StoragePath
                };

                foreach (var directory in requiredDirectories)
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);

                    if (!Directory.Exists(path))
                    {
                        Logger?.Debug("Creating path {Path}", path);
                        Directory.CreateDirectory(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Could not scaffold required directories");
            }
            #endregion

            #region Migrate Database
            using var scope = app.Services.CreateScope();

            using var db = scope.ServiceProvider.GetService<DatabaseContext>();

            if (db.Database.GetPendingMigrations().Any())
            {
                Logger?.Debug("Migrations are pending!");

                var backupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                var dataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LANCommander.db");
                var backupName = Path.Combine(backupPath, $"LANCommander.db.{DateTime.Now.ToString("dd-MM-yyyy-HH.mm.ss.bak")}");

                if (File.Exists(dataSource))
                {
                    Logger?.Debug("Database already exists, backing up as {BackupName}", backupName);
                    File.Copy(dataSource, backupName);
                }

                Logger?.Debug("Migrating database...");
                db.Database.Migrate();
            }
            #endregion

            if (settings.LaunchCount == 0)
            {
                var workingDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

                Logger?.Debug("Current working directory is {WorkingDirectory}", workingDirectory);

                #region Fix Zone Identifier
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        Logger?.Debug("Attempting to fix security zone identifier all files...");

                        var files = Directory.GetFiles(workingDirectory, "*", SearchOption.AllDirectories);

                        foreach (var file in files)
                        {
                            try
                            {
                                var fileInfo = new FileInfo(file);

                                fileInfo.GetDataStream("Zone.Identifier")?.Delete();
                            }
                            catch (Exception ex)
                            {
                                Logger?.Error(ex, "Could not fix zone identifier");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, "Could not get files to fix zone identifier");
                    }
                }
                #endregion

                #region Rename Autoupdater
                var updaterPath = Path.Combine(workingDirectory, "LANCommander.AutoUpdater.exe");

                try
                {
                    if (File.Exists($"{updaterPath}.Update"))
                    {
                        if (File.Exists(updaterPath))
                            File.Delete(updaterPath);

                        File.Move($"{updaterPath}.Update", updaterPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not rename updater");
                }

                updaterPath = Path.Combine(workingDirectory, "LANCommander.AutoUpdater");

                try
                {
                    if (File.Exists($"{updaterPath}.Update"))
                    {
                        if (File.Exists(updaterPath))
                            File.Delete(updaterPath);

                        File.Move($"{updaterPath}.Update", updaterPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not rename updater");
                }
                #endregion
            }

            settings.LaunchCount++;

            SettingService.SaveSettings(settings);

            Logger?.Debug("Starting application...");

            app.Run();

            Logger?.Debug("Closing application...");
        }
    }
}
