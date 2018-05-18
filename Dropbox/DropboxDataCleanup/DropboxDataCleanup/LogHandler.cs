using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Log_Handler
{
    class LogHandler
    {
        /// <summary>
        /// Creates a new log entry using the specified severity level and a system exception
        /// </summary>
        /// <param name="e"></param>
        /// <param name="severity">Using SeverityLevel enumerator</param>
        /// <param name="subject">A brief description of the event</param>
        public static void CreateEntry(Exception e, SeverityLevel severity, string subject)
        {
            LogEntry logEntry = new LogEntry()
            {
                time = DateTime.Now,
                exception = e,
                severity = severity,
                subject = subject
            };

            LogFile.WriteLogEntry(logEntry);
        }

        /// <summary>
        /// Creates a new log entry using the specified severity level
        /// </summary>
        /// <param name="severity">Using SeverityLevel enumerator</param>
        /// <param name="subject">A brief description of the event</param>
        public static void CreateEntry(SeverityLevel severity, string subject)
        {
            LogEntry logEntry = new LogEntry()
            {
                time = DateTime.Now,
                severity = severity,
                subject = subject
            };

            LogFile.WriteLogEntry(logEntry);
        }
    }

    class LogFile
    {
        private static bool isInitialised = false;
        private static string filePath;
        private static LogSession currentSession;

        private static void Initialise()
        {
            string applicationFolder = AppDomain.CurrentDomain.BaseDirectory;
            filePath = applicationFolder + @"UpdateLog.xml";
        }

        private static void CreateLogFile()
        {
            XmlDocument defaultLogFile = new XmlDocument();
            XmlDeclaration xmlDeclaration = defaultLogFile.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = defaultLogFile.DocumentElement;
            defaultLogFile.InsertBefore(xmlDeclaration, root);

            XmlElement rootNode = defaultLogFile.CreateElement("UpdateLog");
            defaultLogFile.AppendChild(rootNode);

            try
            {
                defaultLogFile.Save(filePath);
            }
            catch
            {
                // Sorry, can't log your failure, we have no log file. ¯\_(ツ)_/¯
            }
        }

        private static void WriteSession()
        {
            XmlDocument logFile = new XmlDocument();
            try
            {
                logFile.Load(filePath);
            }
            catch
            {

            }

            logFile.SelectSingleNode("UpdateLog").AppendChild(currentSession.CreateXmlElement(logFile));

            try
            {
                logFile.Save(filePath);
            }
            catch
            {
                
            }
        }

        public static void WriteLogEntry(LogEntry logEntry)
        {
            if (!isInitialised)
            {
                Initialise();
            }

            XmlDocument logFile = new XmlDocument();

            if (!File.Exists(filePath))
            {
                CreateLogFile();
            }
            else
            {
                try
                {
                    logFile.Load(filePath);
                }
                catch
                {

                }

                if (logFile.SelectSingleNode("UpdateLog") == null)
                {
                    CreateLogFile();
                }
            }

            if (currentSession == null)
            {
                currentSession = new LogSession();
                WriteSession();
            }

            try
            {
                logFile.Load(filePath);
            }
            catch
            {

            }

            XmlNode sessionNode = logFile.SelectSingleNode("UpdateLog/LogSession[@StartTime='" + currentSession.startTime.ToString("o") + "']");

            sessionNode.AppendChild(logEntry.CreateXmlElement(logFile));

            try
            {
                logFile.Save(filePath);
            }
            catch
            {

            }
        }
    }

    class LogEntry
    {
        public DateTime time { get; set; }
        public Exception exception { get; set; }
        public SeverityLevel severity { get; set; }
        public string subject { get; set; }
        public string detail { get; set; }

        public XmlElement CreateXmlElement(XmlDocument xmlDoc)
        {
            XmlElement element = xmlDoc.CreateElement("LogEntry");
            
            XmlAttribute timeAttribute = xmlDoc.CreateAttribute("Time");
            timeAttribute.InnerText = time.ToString("o");
            element.Attributes.Append(timeAttribute);
            
            XmlElement severityNode = xmlDoc.CreateElement("Severity");
            severityNode.InnerText = severity.ToString();
            element.AppendChild(severityNode);
            
            XmlElement subjectNode = xmlDoc.CreateElement("Subject");
            subjectNode.InnerText = subject;
            element.AppendChild(subjectNode);
            
            if (detail != null)
            {
                XmlElement detailNode = xmlDoc.CreateElement("Detail");
                detailNode.InnerText = detail;
                element.AppendChild(detailNode);
            }

            if (exception != null)
            {
                XmlElement exceptionMessageNode = xmlDoc.CreateElement("ExceptionMessage");
                exceptionMessageNode.InnerText = exception.Message;
                element.AppendChild(exceptionMessageNode);
                
                XmlElement exceptionFullTextNode = xmlDoc.CreateElement("ExceptionFullText");
                exceptionFullTextNode.InnerText = exception.ToString();
                element.AppendChild(exceptionFullTextNode);
            }
            
            return element;
        }
    }

    class LogSession
    {
        public string machineName { get; }
        public string userName { get; }
        public DateTime startTime { get; }
        public Version currentApplicationVersion { get; }
        public Version currentAutoUpdateDllVersion { get; }

        public LogSession()
        {
            machineName = Environment.MachineName;
            userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            startTime = DateTime.Now;
            currentApplicationVersion = Assembly.GetEntryAssembly().GetName().Version;
            currentAutoUpdateDllVersion = Assembly.LoadFrom("AutoUpdate.dll").GetName().Version;
        }

        public XmlElement CreateXmlElement(XmlDocument xmlDoc)
        {
            XmlElement element = xmlDoc.CreateElement("LogSession");
            
            XmlAttribute startTimeAttribute = xmlDoc.CreateAttribute("StartTime");
            startTimeAttribute.InnerText = startTime.ToString("o");
            element.Attributes.Append(startTimeAttribute);
            
            XmlAttribute currentApplicationVersionAttribute = xmlDoc.CreateAttribute("CurrentApplicationVersion");
            currentApplicationVersionAttribute.InnerText = currentApplicationVersion.ToString();
            element.Attributes.Append(currentApplicationVersionAttribute);
            
            XmlAttribute currentAutoUpdateDllVersionAttribute = xmlDoc.CreateAttribute("CurrentAutoUpdateDllVersion");
            currentAutoUpdateDllVersionAttribute.InnerText = currentAutoUpdateDllVersion.ToString();
            element.Attributes.Append(currentAutoUpdateDllVersionAttribute);
            
            XmlAttribute machineNameAttribute = xmlDoc.CreateAttribute("MachineName");
            machineNameAttribute.InnerText = machineName;
            element.Attributes.Append(machineNameAttribute);
            
            XmlAttribute userNameAttribute = xmlDoc.CreateAttribute("UserName");
            userNameAttribute.InnerText = userName;
            element.Attributes.Append(userNameAttribute);

            return element;
        }
    }
    
    public enum SeverityLevel
    {
        Fatal,  // Data loss is likely to have occurred, or is likely to occur, as a result of this event
        Error,  // Application cannot function correctly following this event, and will likely terminate
        Warn,   // Application was stopped from doing something but can keep running, maybe switched to a backup or wasn't able to load a page
        Info,   // Useful information about what just happened, maybe a service started or a connection was established
        Debug,  // Information useful for technicians or sysadmins to troubleshoot an issue
        Trace   // Application has an itch on its nose that the developer might want to know about
    }
}
