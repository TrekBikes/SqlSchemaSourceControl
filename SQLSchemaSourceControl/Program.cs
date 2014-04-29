﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMO = Microsoft.SqlServer.Management.Smo;
using System.Collections.Specialized;
using Microsoft.SqlServer.Management.Sdk.Sfc;
using Microsoft.SqlServer.Management.Smo.Agent;
using System.IO;
using System.Diagnostics;
using System.Configuration;
using System.Xml.Linq;
using System.Net.Mail;

namespace SQLSchemaSourceControl
{
    enum SourceControlProvider {git, SVN};

    class Program
    {
        private static string _mainFilePath;
        //private static bool _svnAuth;
        //private static string _svnUserName;
        //private static string _svnPassword;

        private static bool _emailOnError;
        private static string _emailServer;
        private static string _emailFrom;
        private static string _emailTo;

        static void Main(string[] args)
        {
            NameValueCollection appSettings = System.Configuration.ConfigurationManager.AppSettings;

            _mainFilePath = appSettings["MainFolderPath"];

            _emailOnError = bool.Parse(appSettings["EmailOnError"]);
            _emailServer = appSettings["EmailServer"];
            _emailFrom = appSettings["EmailFrom"];
            _emailTo = appSettings["EmailTo"];

            System.Console.WriteLine("Config Ok");

            SourceControlProvider scp = (SourceControlProvider)System.Enum.Parse(typeof(SourceControlProvider), appSettings["SourceControl"]);

            SourceControl.ISourceControl sourceControl;

            if (scp == SourceControlProvider.git)
            { 
                sourceControl = new SourceControl.git();
            }
            else if (scp == SourceControlProvider.SVN)
            {
                sourceControl = new SourceControl.SVN();
            }
            else
            {
                throw new Exception("Bad SourceControlProvider");
            }

            try
            {
                XDocument runSteps = XDocument.Load(ConfigurationManager.AppSettings["RunStepsPath"].ToString());

                var LogJobServers = from c in runSteps.Elements("LogServers").Elements("LogJobs").Elements("server")
                                    select (string)c.Attribute("serverName");


                foreach (string serverName in LogJobServers)
                {
                    string srvPath = _mainFilePath + "\\" + serverName;

                    Database.IDatabase db = new Database.SqlServer2008(serverName);

                    NameValueCollection nv;
                    nv = db.GetConfiguration();
                    if (nv != null)
                    {
                        sourceControl.LogObjects(srvPath, "Configuration", nv);
                    }
                    sourceControl.LogObjects(srvPath, "Databases", db.GetDatabases());
                    sourceControl.LogObjects(srvPath, "Logins", db.GetLogins());
                    sourceControl.LogObjects(srvPath, "Jobs", db.GetJobs());
                }

                var LogObjectServers = from c in runSteps.Elements("LogServers").Elements("LogObjects").Elements("server")
                                       select c;


                foreach (var logObjectServer in LogObjectServers)
                {
                    string serverName = (string)logObjectServer.Attribute("serverName");
                    //switch to regex match on database name - need to select
                    Database.IDatabase db = new Database.SqlServer2008(serverName);
                    List<string> databases = db.ListDatabasesForPattern((string)logObjectServer.Attribute("databaseName"));
                    foreach (string dbName in databases)
                    {
                        string dbPath = _mainFilePath + "\\" + serverName + @"\" + dbName;
                        bool newDb = false;

                        if (!Directory.Exists(dbPath))
                        {
                            newDb = true;
                        }

                        if (db.SelectDatabase(dbName))
                        {

                            Console.WriteLine(DateTime.Now.ToString() + " Object Log Start for server " + serverName +
                                              " database " + dbName);

                            try
                            {
                                sourceControl.LogObjects(dbPath, "StoredProcedures", db.GetStoredProcedures());
                                sourceControl.LogObjects(dbPath, "Functions", db.GetFunctions());
                                sourceControl.LogObjects(dbPath, "Views", db.GetViews());
                                sourceControl.LogObjects(dbPath, "Tables", db.GetTables());
                                sourceControl.LogObjects(dbPath, "PartitionFunctions", db.GetPartitionFunctions());
                                sourceControl.LogObjects(dbPath, "PartitionSchemes", db.GetPartitionSchemes());
                                sourceControl.LogObjects(dbPath, "Indexes", db.GetIndexes());
                                sourceControl.LogObjects(dbPath, "Users", db.GetUsers());
                                if (newDb)
                                {
                                    sourceControl.Add(dbPath);
                                }
                            }
                            catch
                            {
                                // eh.  db is usually unavailable.
                            }
                        }
                    }
                }


                //git is one giant repo.  SVN is usually one working copy per "server"
                if (scp == SourceControlProvider.git)
                {
                    sourceControl.Commit("");
                }
                else if (scp == SourceControlProvider.SVN)
                {
                    // loop through all dirs in main folder and commit..
                    string[] dirs = Directory.GetDirectories(_mainFilePath);

                    foreach (string dir in dirs)
                    {
                        sourceControl.Commit(dir);
                    }
                }

                Console.WriteLine("done");
                //Console.ReadLine();

            }
            catch (Exception ex)
            {
                if (_emailOnError)
                {
                    string MailServer = _emailServer;
                    string From = _emailFrom;
                    string To = _emailTo;
                    string Subject = "SQLSchemaSourceControl Failed";
                    string Body = ex.Message + " " + ex.InnerException + " " + ex.StackTrace;
                    bool IsHtml = true;
                    SendMail(MailServer, From, To, Subject, Body, IsHtml);

                }
                else
                {
                    Console.WriteLine(ex.Message + " " + ex.InnerException + " " + ex.StackTrace);
                    throw ex;
                }


            }
        }

        public static void SendMail(string MailServer, string From, string To, string Subject, string Body, bool IsHtml)
        {
            MailMessage msg;
            using (msg = new MailMessage())
            {
                msg.To.Add(To);
                msg.From = new MailAddress(From);
                msg.Subject = Subject;
                msg.Body = Body;
                msg.IsBodyHtml = IsHtml;

                var smtpClient = new SmtpClient(MailServer);

                smtpClient.Send(msg);
            }
        }

    }
}
