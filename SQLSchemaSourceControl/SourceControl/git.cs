using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Specialized;
using System.IO;
using System.Configuration;

namespace SQLSchemaSourceControl.SourceControl
{
    class git : ISourceControl
    {
        bool _gitAuth = false;
        string _username;
        string _password;
        string _path;
        string _repoRoot;

        public git() 
        {
            _username = System.Configuration.ConfigurationManager.AppSettings["gitUsername"];
            _password = System.Configuration.ConfigurationManager.AppSettings["gitPassword"];
            _gitAuth = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["gitAuthentication"]);
            _path = System.Configuration.ConfigurationManager.AppSettings["gitPath"];
            _repoRoot = System.Configuration.ConfigurationManager.AppSettings["MainFolderPath"];
        }

        public bool Add(string Path)
        {
            return ExecuteCommand("add", Path);
        }

        public bool Delete(string Path) {
            return ExecuteCommand("rm", Path);
        }

        public bool Update(string Path)
        {
            return true;
        }

        public bool Commit(string Path)
        {
            return ExecuteCommand("commit -a", null, "SQLSchemaSourceControl Commit");
        }

        private bool ExecuteCommand(string Command, string Path)
        {
            return ExecuteCommand(Command, Path, null);
        }

        private bool ExecuteCommand(string Command, string Path, string Message) 
        {
            ProcessStartInfo gitCmd = new ProcessStartInfo();
            gitCmd.FileName = _path;
            gitCmd.CreateNoWindow = true;
            gitCmd.WindowStyle = ProcessWindowStyle.Hidden;
            gitCmd.WorkingDirectory = _repoRoot;

            if (Message != null)
            {
                Message = string.Format(" --message \"{0}\"", Message);
            }
            else
            {
                Message = "";
            }

            if (Path != null)
            {
                gitCmd.Arguments = string.Format("{0} \"{1}\"{2} ", Command, Path, Message);
            }
            else
            {
                gitCmd.Arguments = string.Format("{0} {1} ", Command, Message);
            }

            //if (_gitAuth)
            //{
            //    gitCmd.Arguments = string.Format("{0} \"{1}\"{2} --username {3} --password {4}", Command, Path, Message, _username, _password);
            //}
            //else
            //{
            //   gitCmd.Arguments = string.Format("{0} \"{1}\"{2} ", Command, Path, Message);
            //}

            System.Console.WriteLine(_path + " " + gitCmd.Arguments);

            Process pCmd = Process.Start(gitCmd);

            //Wait for the process to end.
            pCmd.WaitForExit();
            return true;
        }

        public bool LogObjects(string BasePath, string ObjectTypeName, NameValueCollection Objects)
        {
            bool newFolder = false;
            string fullPath = BasePath + @"\" + ObjectTypeName;
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                newFolder = true;
            }

            DirectoryInfo d = new DirectoryInfo(fullPath);
            FileInfo[] files = d.GetFiles("*.sql");

            List<string> fileNames = new List<string>();
            List<string> objectFileNames = new List<string>();

            foreach (FileInfo file in files)
            {
                fileNames.Add(file.Name);
            }

            foreach (string key in Objects)
            {
                string filename = fullPath + "\\" + key.Replace(@"/", "_").Replace(@"\", "_") + ".sql";

                objectFileNames.Add(key.Replace(@"/", "_").Replace(@"\", "_") + ".sql");

                string newFile = Objects[key];

                bool writeFile = true;
                bool addFile = false;

                if (!newFolder)
                {

                    if (!File.Exists(filename))
                    {
                        addFile = true;
                    }
                    else
                    {

                        StreamReader sr = new StreamReader(filename);

                        string existingFile = sr.ReadToEnd();
                        sr.Close();

                        if (existingFile != newFile)
                        {
                            Update(filename);
                        }
                        else
                        {
                            writeFile = false;
                        }
                    }
                }

                if (writeFile)
                {
                    StreamWriter sw = new StreamWriter(filename, false);

                    sw.Write(newFile);
                    sw.Flush();
                    sw.Close();
                }

                if (addFile)
                {
                    Add(filename);
                }
            }

            if (newFolder)
            {
                Add(fullPath);
            }

            // handle the deletes for any files - stored procedures
            foreach (string file in fileNames)
            {
                if (objectFileNames.IndexOf(file) == -1)
                {
                    Delete(fullPath + @"\" + file);
                }
            }
            return true;
        }
    }
}
