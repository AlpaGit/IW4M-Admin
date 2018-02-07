﻿
#define USINGMEMORY
using System;
using System.Runtime.InteropServices;
using SharedLibrary;
using System.Threading.Tasks;
using System.IO;
using SharedLibrary.Objects;

#if DEBUG
using SharedLibrary.Database;
#endif

namespace IW4MAdmin
{
    class Program
    {
        static public double Version { get; private set; }
        static private ApplicationManager ServerManager;

        static void Main(string[] args)
        {
            System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;

            Version = 1.6;
            handler = new ConsoleEventDelegate(OnProcessExit);
            SetConsoleCtrlHandler(handler, true);

            //double.TryParse(CheckUpdate(), out double latestVersion);
            Console.WriteLine("=====================================================");
            Console.WriteLine(" IW4M ADMIN");
            Console.WriteLine(" by RaidMax ");
            Console.WriteLine($" Version {Version}");
            Console.WriteLine("=====================================================");

            try
            {
                CheckDirectories();

                ServerManager = ApplicationManager.GetInstance();
                ServerManager.Init();

                Task.Run(() =>
                {
                    String userInput;
                    Player Origin = ServerManager.GetClientService().Get(1).Result.AsPlayer();

                    do
                    {
                        userInput = Console.ReadLine();

                        if (userInput?.ToLower() == "quit")
                            ServerManager.Stop();

                        if (ServerManager.Servers.Count == 0)
                            return;

                        Origin.CurrentServer = ServerManager.Servers[0];
                        Event E = new Event(Event.GType.Say, userInput, Origin, null, ServerManager.Servers[0]);
                        ServerManager.Servers[0].ExecuteEvent(E);
                        Console.Write('>');

                    } while (ServerManager.Running);
                });

            }

            catch (Exception e)
            {
                Console.WriteLine($"Fatal Error during initialization: {e.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            try
            {
                ServerManager.Start();
            }

            catch (Exception e)
            {
                throw e;
            }
        }

        static ConsoleEventDelegate handler;

        static private bool OnProcessExit(int e)
        {
            try
            {
                ServerManager.Stop();
                return true;
            }

            catch
            {
                return true;
            }
        }

        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);

        static void CheckDirectories()
        {
            if (!Directory.Exists("Lib"))
                throw new Exception("Lib folder does not exist");

            if (!Directory.Exists("Config"))
            {
                Console.WriteLine("Warning: Config folder does not exist");
                Directory.CreateDirectory("Config");
            }

            if (!Directory.Exists("Config/Servers"))
                Directory.CreateDirectory("Config/Servers");

            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            if (!Directory.Exists("Database"))
                Directory.CreateDirectory("Database");

            if (!Directory.Exists("Plugins"))
                Directory.CreateDirectory("Plugins");
        }
    }
}
