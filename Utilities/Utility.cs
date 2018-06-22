using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;

namespace NtpTimeSyncService.Utilities
{
    public static class Utility
    {
        public static long Ping(string IpAddress)
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            // Create a buffer of 32 bytes of data to be transmitted.
            byte[] buffer = Encoding.ASCII.GetBytes("abcdefghijklmnopqrstuvwxyz012345");
            int timeout = 60;
            PingReply reply = pingSender.Send(IpAddress, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
            {
                return reply.RoundtripTime; // $"OK. Response Time:{reply.RoundtripTime} ms";
            }
            else
            {
                return -1; // $"Failed to reply.";
            }
        }
        
        public static List<string> ReadTxtFileContentAtPath(string Path)
        {
            List<string> lstRead = new List<string>();
            try
            {
                using (StreamReader sr = new System.IO.StreamReader(Path))
                {
                    while (sr.Peek() >= 0)
                    {
                        lstRead.Add(sr.ReadLine());
                        //Console.WriteLine(line)
                    }
                }
            }
            catch (Exception)
            {
                Debug.Print("Failed to read at " + Path);
                lstRead = null;
            }
            return lstRead;
        }

        public static bool CreateTxtFileWithContentAtPath(string Path, string content)
        {
            var bRet = false;
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            // Create a file to write to.
            StreamWriter sw = File.CreateText(Path);
            try
            {
                sw.WriteLine(content);
                sw.Flush();
                bRet = true;
            }
            catch (Exception)
            {
               // Debug.Print(ex.Message);
                bRet = false;
            }
            finally
            {
                sw.Close();
            }

            return bRet;
        }

        public static void ConsoleWriteAt(string s, int x, int y)
        {
            try
            {
                var orgX= Console.CursorLeft;
                var orgY= Console.CursorTop;
                Console.SetCursorPosition(x,y);
                Console.Write(s);
                Console.SetCursorPosition(orgX, orgY);
            }
            catch (ArgumentOutOfRangeException e)
            {
                Console.Clear();
                Console.WriteLine(e.Message);
            }
        }

        public static bool WriteToFile(string strPath, string strFile, string msg)
        {
            //Create temp file to avoid file conflict between two software accessing the same file 
            var tempFileName = Path.Combine(strPath, Path.GetRandomFileName());
            var strLog = msg + Environment.NewLine;
            var sw = System.IO.File.CreateText(tempFileName);
            using (sw)
            {
                sw.Write(strLog);
                sw.Flush();
            }
            System.IO.File.Copy(tempFileName, strPath + strFile,true);
            System.IO.File.Delete(tempFileName);
            return true;
        }

        public static bool WriteToFile(string strFile, string msg)
        {
            var strLog = msg + Environment.NewLine;
            var sw = System.IO.File.AppendText(strFile);
            using (sw)
            {
                sw.Write(strLog);
                sw.Flush();
            }
            return true;
        }

        public static string FieldsToString(Type type, object obj)
        {
            var str = string.Empty;
            var typ = type;
            var fields = typ.GetFields();
            foreach (var field in fields)
            {
                var temp = field.GetValue(obj); // Get value
                if (temp is int) // See if it is an integer.
                {
                    var value = (int)temp;
                    str += field.Name + ":'" + value + "', ";
                }
                else if (temp is string) // See if it is a string.
                {
                    var value = temp as string;
                    str += field.Name.Trim() + ":'" + value.Trim() + "', ";
                }
            }
            return "{" + str.TrimEnd(',', ' ') + "}";
        }

        public static string AutoRenameFilename(FileInfo file)
        {
                var filename = string.IsNullOrEmpty(file.Extension)?file.Name:file.Name.Replace(file.Extension, string.Empty);
                var dir = file.Directory.FullName;
                var ext = file.Extension;

                if (!file.Exists) return ( Path.Combine(dir , filename + ext));
                var count = 0;
                string added;

                do
                {
                    count++;
                    added = "(" + count + ")";
                } while (File.Exists(dir + "\\" + filename + " " + added + ext));

                filename += " " + added;
                return (Path.Combine(dir, filename + ext));
       
        }

       public static string Space(int count)
        {
            return "".PadLeft(count);
        }

       public static bool CopyFileEx(string srcFullFileName, string dstFullFileName)
       {
            var isSucess = false;

            if (File.Exists(dstFullFileName))
            {
                var srcFileSize = new FileInfo(srcFullFileName).Length;
                var dstFileSize = new FileInfo(dstFullFileName).Length;
                if (srcFileSize != dstFileSize)
                {
                    File.Copy(srcFullFileName, dstFullFileName, true);
                    isSucess = true;
                }
            }
            else
            {
                File.Copy(srcFullFileName, dstFullFileName, true);
                isSucess = true;
            }
            return isSucess;
       }
        
       public static string UdpToString(byte[] receivedBytes)
        {
            var bytesAsString = string.Empty;
            try
            {
                bytesAsString = Encoding.ASCII.GetString(receivedBytes);
                if (string.IsNullOrEmpty(bytesAsString)) return string.Empty;
                
            }
            catch (Exception ex)
            {
                Debug.Print("3." + ex.Message);
            }
            return bytesAsString;
        }
        
       public static bool IsValidUdpFormat(string input)
       {
            const string pattern = @"[A-Za-z0-9\{\}\:\'\/'_\,\.\-\+]";
            return (System.Text.RegularExpressions.Regex.Matches(input, pattern).Count == input.Length);
       }

    }


}
