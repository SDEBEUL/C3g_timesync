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



namespace C3G_Timesync
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "VOLVO Comau C3G Timesync Build by SDEBEUL";
            Console.BufferHeight = 25;
            //
            Console.WriteLine("Gadata up and running current timestamp= " +  GetTimeFromGadata().ToString());
            //build file sytem watch 
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = @"\\gnl9011101\6308-APP-NASROBOTBCK0001\robot_ga\ROBLAB\99999R99\Tsync";
            watcher.Filter = "*.TSF"; ;
            watcher.InternalBufferSize = (watcher.InternalBufferSize * 2); //2 times default buffer size 
            watcher.Error += new ErrorEventHandler(OnError);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnDeleted);
            watcher.EnableRaisingEvents = true;
            //stop from closing
            Console.ReadLine();
            Console.Write("\b \b"); //Test 

        }


        // Event handeler for error event in wacther (auto restart)
        private static void OnError(object source, ErrorEventArgs e)
        {

            Trace.Listeners.Add(new TextWriterTraceListener("C3g_TimesyncDebug.log"));
            Trace.AutoFlush = true;
            Trace.Indent();
            Trace.Unindent();
            Trace.Flush();
            //  Show that an error has been detected.
            Console.WriteLine("The FileSystemWatcher has detected an error");
            Trace.WriteLine("The FileSystemWatcher has detected an error");
            //  Give more information if the error is due to an internal buffer overflow. 
            if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                Console.WriteLine(("The file system watcher experienced an internal buffer overflow: " + e.GetException().Message));
                Trace.WriteLine(("The file system watcher experienced an internal buffer overflow: " + e.GetException().Message));
            }
            Console.WriteLine("System will restart in 30 seconds");
            System.Threading.Thread.Sleep(30000);
            //restart application
            var fileName = Assembly.GetExecutingAssembly().Location;
            System.Diagnostics.Process.Start(fileName);
            Environment.Exit(0);
        }

 // Event handeler for robot puts file on server
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            System.Threading.Thread.Sleep(1000); //anders leest hij lege file
            string text = System.IO.File.ReadAllText(e.FullPath);
            text = text.TrimEnd(new char[] { '\r', '\n' });
            DateTime servertime = GetTimeFromGadata();
            System.Console.WriteLine("Robot: " + e.Name + " Robottime= " + text + " Send: " + servertime.ToString("HH:mm:ss") + " " + servertime.ToString("d-MM-yy"));
            System.IO.File.WriteAllText(e.FullPath, servertime.AddSeconds(16).ToString("HH:mm:ss") + Environment.NewLine + servertime.ToString("d-MM-yy"));
            LogTsynctoGADATA(e.Name.Substring(0, 8), ConvertComauDate(text));
        }

 //Event handeler for robot confirms sync
        private static void OnDeleted(object source, FileSystemEventArgs e)
        {
            Console.WriteLine("Robot: " + e.Name + " Confirms Sync");
            LogConfirmtoGADATA(e.Name.Substring(0,8));
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
                Console.WriteLine("Converted '{0}' to {1}.", ad_date, parsedDate);
                return parsedDate;
            }
            else
            {
                Console.WriteLine("Unable to convert '{0}' to a date and time.", ad_date);
                return parsedDate;
            }
        }
 // get time form server
        static DateTime GetTimeFromGadata()
        {
            //open connection to gadata
            using (SqlConnection Gadataconn = new SqlConnection("user id=GADATA; password=GADATA987; server=SQLA001.gen.volvocars.net;" +
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
                        return GAtime;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    return DateTime.Now;
                }

 
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
 //log activity to gadata
        static void LogTsynctoGADATA(string ai_robot, DateTime ad_robotdate)
        {
            //open connection to gadata
            using (SqlConnection Gadataconn = new SqlConnection("user id=GADATA; password=GADATA987; server=SQLA001.gen.volvocars.net;" +
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
            using (SqlConnection Gadataconn = new SqlConnection("user id=GADATA; password=GADATA987; server=SQLA001.gen.volvocars.net;" +
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
    }
}
    

