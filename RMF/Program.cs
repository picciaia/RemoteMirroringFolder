//MIT License

//Copyright(c) 2018 Daniele Picciaia

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using Microsoft.Synchronization;
using Microsoft.Synchronization.Files;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RMF
{
    /// <summary>
    /// Starting object with main entry point
    /// This appplication can be started both as a standard windows console app or as a windows service
    /// </summary>
    public class Program
    {
        private static bool consoleMode;        // Operating mode flag: TRUE when running in console mode, FALSE otherwise
        private static RMFManager manager = null;
        /// <summary>
        /// Entry point of the application
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            consoleMode = Environment.UserInteractive;
            Logger.Enabled = true;
            Logger.LogOnFile = true;
            Logger.CurrentVerbosityInfo = (VerbosityInfoLevel)AppSettings.Default.LoggerVerbosity;

            Logger.Log("RemoteMirroringFolder - ver. {0} started", LogInfo.Info, VerbosityInfoLevel.V1,
                Assembly.GetExecutingAssembly().GetName().Version.ToString());

            if (!consoleMode)
            {
                using (var service = new RMFService())
                    ServiceBase.Run(service);
            }
            else
            {
                var prog = new Program();
                prog.Start(args);
                if (consoleMode)
                {
                    Console.WriteLine("Press Q to exit");
                    while (Console.ReadKey().Key != ConsoleKey.Q) { }
                    prog.Stop();
                }
            }
        }
        /// <summary>
        /// Start the application/service
        /// </summary>
        /// <param name="args"></param>
        public void Start(string[] args)
        {
            manager = new RMFManager();
            manager.Start();
        }
        /// <summary>
        /// Stop the application/service
        /// </summary>
        public void Stop()
        {
            manager.Stop();
        }

        #region Nested classes to support running as service
        public const string ServiceName = "RemoteMirroringFolderService";
        public class RMFService : ServiceBase
        {
            Program prog = new Program();
            public RMFService()
            {
                ServiceName = Program.ServiceName;
            }
            protected override void OnStart(string[] args)
            {
                prog.Start(args);
            }
            protected override void OnStop()
            {
                prog.Stop();
            }
        }
        #endregion
    }

    #region Service installer, required by installutil to install as a windows service
    //Install the service using:
    //  installutil /i rmf.service.exe

    //Uninstall using:
    //installutil /u rmf.service.exe

    [RunInstaller(true)]
    public class RMFServiceInstaller : System.Configuration.Install.Installer
    {
        ServiceProcessInstaller process = new ServiceProcessInstaller();
        ServiceInstaller serviceAdmin = new ServiceInstaller();
        public RMFServiceInstaller()
        {
            process.Account = ServiceAccount.LocalSystem;
            serviceAdmin.StartType = ServiceStartMode.Automatic;
            serviceAdmin.ServiceName = Program.ServiceName;
            serviceAdmin.DisplayName = "RemoteMirroringFolder service";
            serviceAdmin.Description = "Remote Folder Mirroring based on Microsoft Sync platform";
            Installers.Add(process);
            Installers.Add(serviceAdmin);
        }
    }

    #endregion

}
