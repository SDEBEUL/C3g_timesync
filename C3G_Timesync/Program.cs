using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SqlClient;
using System.Globalization;
using System.Reflection;
using System.Diagnostics;
using System.Text.RegularExpressions;



namespace C3G_Timesync
{

    static class Buffer
    {
        static List<string> _Logbuffer; // Static List instance
        static Buffer() { _Logbuffer = new List<string>(); }
        public static void Record(string value) { _Logbuffer.Add(value); }
        public static void Delete(string value) { _Logbuffer.Remove(value); }
        public static Int32 Count() { return _Logbuffer.Count(); }
        public static List<string> getbuffer() { return _Logbuffer; }
        public static void Display() { foreach (var value in _Logbuffer) { Console.WriteLine(value); } }
        public static bool Contains(string file) { if (_Logbuffer.Contains(file)) { return true; } else { return false; } }
    }

    public class ConsoleSpiner
    {
        int counter;
        public ConsoleSpiner()
        {
            counter = 0;
        }
        public void Turn()
        {
            counter++;
            switch (counter % 4)
            {
                case 0: Console.Write("/"); break;
                case 1: Console.Write("-"); break;
                case 2: Console.Write("\\"); break;
                case 3: Console.Write("-"); break;
            }
            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
        }
    } 

    static class Debug
    {
        public static void Init()
        {
            Trace.Listeners.Add(new TextWriterTraceListener("C3g_TimesyncDebug.log"));
            Trace.AutoFlush = true;
            Trace.Indent();
            Trace.Unindent();
            Trace.Flush();
        }
        public static void Restart()
        {
            Console.WriteLine("System will restart in 10 seconds");
            System.Threading.Thread.Sleep(10000);
            var fileName = Assembly.GetExecutingAssembly().Location;
            System.Diagnostics.Process.Start(fileName);
            Environment.Exit(0);
        }
        public static void Message(string ls_part, string ls_message)
        {
            Trace.WriteLine("DT: " + System.DateTime.Now + " P: " + ls_part + " M: " + ls_message);
            Console.WriteLine("DT: " + System.DateTime.Now + " P: " + ls_part + " M: " + ls_message);
            using (EventLog eventlog = new EventLog("Application"))
            {
                eventlog.Source = "Application";
                eventlog.WriteEntry(ls_message, EventLogEntryType.Information, 101, 1);
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            //build logtracer
            Trace.Listeners.Add(new TextWriterTraceListener("C3g_TimesyncDebug.log"));
            Debug.Init();
            Debug.Message("INFO", "System restarted");
            //
            Console.Title = "VOLVO Comau C3G Timesync Build by SDEBEUL 17w20d04";
            Console.BufferHeight = 25;
            //
            Console.WriteLine("Gadata up and running current timestamp= " +  GetTimeFromGadata().ToString());
            //build file sytem watch 
            //*****************************************************************************************************************************************
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = @"\\gnlsnm0101.gen.volvocars.net\6308-APP-NASROBOTBCK0001\robot_ga\ROBLAB\99999R99\Tsync";
            watcher.Filter = "*.TSF"; ;
            watcher.InternalBufferSize = (watcher.InternalBufferSize * 2); //2 times default buffer size 
            watcher.Error += new ErrorEventHandler(OnError);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnDeleted);
            watcher.EnableRaisingEvents = true;
            //*****************************************************************************************************************************************
            //MAIN 
            ConsoleSpiner spin = new ConsoleSpiner();
            while (true)
            {
                System.Threading.Thread.Sleep(500);
                try
                {
                    if (Buffer.Count() == 0) { Console.Write("\r System ready (buffer empty)                               "); spin.Turn(); }
                    else
                    {
                        List<string> localbuffer = Buffer.getbuffer();
                        foreach (string file in localbuffer.ToList())
                        {
                            try
                            {
                                if (Buffer.Contains(file) && IsFileReady(file)) 
                                {
                                    Console.WriteLine("");
                                    HandleTsyncFileCreate(file);
                                    Buffer.Delete(file);
                                }
                                else if (!File.Exists(file)) { Buffer.Delete(file); Debug.Message("FileNotExistWhileInBuffer", file.Substring(Math.Max(0, file.Length - 40))); }
                            }
                            catch (Exception ex) { Debug.Message("Buffersweep", file.Substring(Math.Max(0, file.Length - 40)) + " msg: " + ex.Message); }
                        }
                    }
                }
                catch (Exception ex) { Debug.Message("GeneralCatch", " msg: " + ex.Message); }
            }

        }
 //Event handeler for error from filewatcher
        private static void OnError(object source, ErrorEventArgs e) { Debug.Restart(); }
//Event handeler for robot creates file  (adds to buffer)
        private static void OnChanged(object source, FileSystemEventArgs e) { Buffer.Record(e.FullPath); }
 //Event handeler for robot confirms sync
        private static void OnDeleted(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("");
            Debug.Message("Event", "Robot: " + e.Name + " Confirms Sync");
            LogConfirmtoGADATA(e.Name.Substring(0, 8));
        }
 // handeler for robot puts file on server
        private static void HandleTsyncFileCreate(string fullfilepath)
        {
            string text = System.IO.File.ReadAllText(fullfilepath);
            text = text.TrimEnd(new char[] { '\r', '\n' });
            DateTime servertime = GetTimeFromGadata();
            Debug.Message("Event", "Robot: " + Path.GetFileNameWithoutExtension(fullfilepath) + " Robottime= " + text + " Send: " + servertime.ToString("HH:mm:ss") + " " + servertime.ToString("d-MM-yy"));
            System.IO.File.WriteAllText(fullfilepath, servertime.AddSeconds(16).ToString("HH:mm:ss") + Environment.NewLine + servertime.ToString("d-MM-yy"));
            LogTsynctoGADATA(Path.GetFileNameWithoutExtension(fullfilepath), ConvertComauDate(text));
        }
 //function to convert a comau date string to datetime
        static DateTime ConvertComauDate(String ad_date)
        {
            CultureInfo ci = CultureInfo.CreateSpecificCulture("en-GB");
            DateTimeFormatInfo dtfi = ci.DateTimeFormat;
            dtfi.AbbreviatedMonthNames = new string[] { "JAN", "FEB", "MAR", 
                                                  "APR", "MAY", "JUN", 
                                                  "JUL", "AUG", "SEP", 
                                                  "OCT", "NOV", "DEC", "" };
            dtfi.AbbreviatedMonthGenitiveNames = dtfi.AbbreviatedMonthNames;
            // ad_date = "1-OCT-14 17:40:45"; //datepatern that would be provided 
            string pattern = "d-MMM-yy HH:mm:ss";
            DateTime parsedDate;
            if (DateTime.TryParseExact(ad_date, pattern, new CultureInfo("en-GB"), DateTimeStyles.AllowWhiteSpaces, out parsedDate))
            {
               // Console.WriteLine("Converted '{0}' to {1}.", ad_date, parsedDate);
                return parsedDate;
            }
            else
            {
                Console.WriteLine("Unable to convert '{0}' to a date and time.", ad_date);
                return parsedDate;
            }
        }
 //get time form server
        static DateTime GetTimeFromGadata()
        {
            //open connection to gadata
            using (SqlConnection Gadataconn = new SqlConnection("user id=VCSCTimesync; password=VCSCTimesync; server=SQLA001.gen.volvocars.net;" +
                                                      "Trusted_Connection=no; database=gadata; connection timeout=30"))
            {
                try
                {
                    Gadataconn.Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                try
                {
                    using (SqlCommand myCommand = new SqlCommand("SELECT getdate()", Gadataconn))
                    {
                        myCommand.ExecuteNonQuery();
                        Object returnValue = myCommand.ExecuteScalar();
                        DateTime GAtime = DateTime.Parse(returnValue.ToString());
                        try
                        {
                            Gadataconn.Close();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        return GAtime;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return DateTime.Now;
                }
            }

        }
 //log activity to gadata
        static void LogTsynctoGADATA(string ai_robot, DateTime ad_robotdate)
        {
            //open connection to gadata
            using (SqlConnection Gadataconn = new SqlConnection("user id=VCSCTimesync; password=VCSCTimesync; server=SQLA001.gen.volvocars.net;" +
                                                      "Trusted_Connection=no; database=gadata; connection timeout=30"))
            {
                try
                {
                    Gadataconn.Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                try
                {
                    using (SqlCommand myCommand = new SqlCommand("INSERT INTO [GADATA].[RobotGA].[L_timesync] (Robot, _timestamp, Robottime) " +
                                                                        "Values (@Robot, getdate(), @Robottimestamp)", Gadataconn))
                    {
                        myCommand.Parameters.AddWithValue("@Robot", ai_robot);
                        string SqlDTformat = "yyyy-MM-dd HH:mm:ss";
                        myCommand.Parameters.AddWithValue("@Robottimestamp", ad_robotdate.ToString(SqlDTformat));
                        myCommand.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                //close gadata
                try
                {
                    Gadataconn.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

        }
 //Confirms robot act to gadata (updates the last _timestamp record 
        static void LogConfirmtoGADATA(string ai_robot)
        {
           //open connection to gadata
            using (SqlConnection Gadataconn = new SqlConnection("user id=VCSCTimesync; password=VCSCTimesync; server=SQLA001.gen.volvocars.net;" +
                                                      "Trusted_Connection=no; database=gadata; connection timeout=30"))
            {
                try
                {
                    Gadataconn.Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            try
            {
                using (SqlCommand myCommand = new SqlCommand("UPDATE [GADATA].[RobotGA].[L_timesync] SET RobotAcktime = getdate() " +
                                                      "WHERE _Timestamp = (select top (1) _Timestamp from [GADATA].[RobotGA].[L_timesync] " +
                                                      "where Robot = @Robot ORDER BY _Timestamp DESC)", Gadataconn)) 
                {
                    myCommand.Parameters.AddWithValue("@Robot", ai_robot);
                    myCommand.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            //close gadata
            try
            {
                Gadataconn.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            }
        }
 //check if file is accesable
        public static bool IsFileReady(String sFilename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(sFilename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (inputStream.Length > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
    

