using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;
namespace DaemonProcess
{
    internal class Program
    {
        class Logger
        {
            private string _logDirectory;

            public Logger(string logDirectory)
            {
                _logDirectory = logDirectory;
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }

            public void WriteLog(string message)
            {
                DateTime date=DateTime.Now;
                string logFileName = GetLogFileName(date);
                string logFilePath = Path.Combine(_logDirectory, logFileName);
                var msg = $"{date:yyyy-MM-dd HH:mm:ss} - {message}";
                Console.WriteLine(msg);
                using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                {
                    writer.WriteLine(msg);
                }
            }

            private string GetLogFileName(DateTime date)
            {
                return $"{date:yyyy-MM-dd}.log";
            }
        }
        static bool ArePathsEqual(string path1, string path2)
        {
            try
            {
                string fullPath1 = Path.GetFullPath(new Uri(path1).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath2 = Path.GetFullPath(new Uri(path2).LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                return string.Equals(fullPath1, fullPath2, StringComparison.OrdinalIgnoreCase);
            }
            catch (UriFormatException)
            {
                // 如果路径不是有效的URI，则直接比较原始字符串（这通常不是一个好主意，因为可能会漏掉一些情况）
                // 或者你可以在这里添加额外的错误处理逻辑
                return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
            }
        }
        public class appConfig
        {
            public string path { get; set; }
            public string arg { get; set; }
            public int dely { get; set; } = 300;
            public int process_id { get; set; } = 0;
            public string process_name { get; set; }
            public int time { get; set; } = 0;
        }
        private static void TimerCallbackMethod(Object o)
        {
            // 这里是定时器触发时执行的代码
            
            foreach(var app in apps)
            {
                app.time -= 5; 
                if (app.time <= 0)
                {
                    Console.WriteLine("checking {0} {1}", app.path,DateTime.Now.ToString());
                    app.time = app.dely;
                    //检查
                    if (app.process_id >0 )
                    {
                        try
                        {
                            var process = Process.GetProcessById(app.process_id);
                            if (process != null)
                            {
                                ProcessStartInfo startInfo = process.StartInfo;
                                string processName = process.ProcessName;
                                string fileName = startInfo.FileName;

                                // 如果FileName不包含完整路径，则尝试使用MainModule.FileName
                                if (string.IsNullOrEmpty(fileName) || !Path.IsPathRooted(fileName))
                                {
                                    try
                                    {
                                        fileName = process.MainModule.FileName;
                                    }
                                    catch (Win32Exception)
                                    {
                                        // 如果没有足够的权限访问MainModule，则忽略该进程
                                        continue;
                                    }
                                }
                                if (!ArePathsEqual(fileName, app.path))
                                {
                                    app.process_id = 0;
                                }
                            }

                        }
                        catch
                        {
                            app.process_id = 0;
                        }
                       
                    }
                    if(app.process_id == 0)
                    {
                        //重启
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = app.path,
                            // 如果需要传递命令行参数，可以在这里设置
                            Arguments = app.arg,
                            // 设置是否使用操作系统外壳程序启动进程（例如，是否通过cmd.exe启动）
                            // UseShellExecute = true, 
                            // 设置是否将进程的窗口隐藏
                            // CreateNoWindow = true, 
                            // 设置工作目录（可选）
                            // WorkingDirectory = @"C:\Some\Directory",
                            // 重定向标准输入/输出/错误流（如果需要与进程交互）
                            // RedirectStandardInput = true,
                            // RedirectStandardOutput = true,
                            // RedirectStandardError = true,
                        };
                        logger.WriteLog(string.Format("start {0}", app.path)); 
                        // 启动进程并获取进程对象
                        using (Process process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                app.process_id = process.Id; 
                            }
                            else
                            {
                                Console.WriteLine("err {0} {1}", app.path, DateTime.Now.ToString()); 
                              
                            }
                        } 
                    } 
                }

            } 

        }
        private static Logger logger = null;
 
        private static Timer _timer;
        private static List<appConfig>  apps = new List<appConfig>();
        static void Main(string[] args)
        {
            Console.WriteLine("Load Processes...");
            apps.Clear();
            var logPath = string.Format(@"{0}\log", AppDomain.CurrentDomain.BaseDirectory);
            logger = new Logger(logPath);
            //获取配置
            var dir = string.Format(@"{0}\apps", AppDomain.CurrentDomain.BaseDirectory);
            if (System.IO.Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    var lines = System.IO.File.ReadAllLines(file);
                    if (lines.Length > 0)
                    {
                        var sps = lines[0].Split(' ').ToList();
                        var path = sps.FirstOrDefault();

                        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                        {
                            sps.RemoveAt(0);
                            var dely = 300;
                            if (lines.Length > 1)
                            {
                                int.TryParse(lines[1], out dely);
                            }
                            apps.Add(new appConfig() { path = path, arg = string.Join(" ", sps), dely = dely });
                        }

                    }

                }
            }
            if (apps.Count > 0)
            {
                // 获取当前系统上所有的进程
                Process[] processes = Process.GetProcesses();
                // 遍历所有进程
                foreach (var process in processes)
                {
                    try
                    {
                        // 避免访问已被终止的进程
                        if (!process.HasExited)
                        {
                            // 获取进程的启动信息
                            ProcessStartInfo startInfo = process.StartInfo;  
                            string processName = process.ProcessName;
                            string fileName = startInfo.FileName;

                            // 如果FileName不包含完整路径，则尝试使用MainModule.FileName
                            if (string.IsNullOrEmpty(fileName) || !Path.IsPathRooted(fileName))
                            {
                                try
                                {
                                    fileName = process.MainModule.FileName;
                                }
                                catch (Win32Exception)
                                {
                                    // 如果没有足够的权限访问MainModule，则忽略该进程
                                    continue;
                                }
                            }
                            var index = apps.FindIndex((ee) =>
                            {
                                return ArePathsEqual(ee.path, fileName);
                            });
                            if (index >= 0)
                            {
                                apps[index].process_id = process.Id;
                                apps[index].process_name = processName;
                                apps[index].time = apps[index].dely;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 捕获并处理任何异常（例如，进程可能在你尝试访问它时终止）
                        // Console.WriteLine($"Error accessing process {process.ProcessName}: {ex.Message}");
                    }
                }
            }
            foreach (var app in apps)
            {
                Console.WriteLine("Monit "+app.path);
            }
            // 初始化定时器，设置初始延迟为0毫秒，周期为1000毫秒（1秒）
            _timer = new Timer(TimerCallbackMethod, null, 0, 5000);
            //保持主线程运行，以便定时器可以触发回调
            Console.WriteLine("Press Enter to exit the program.");
            Console.ReadLine(); 
            // 停止定时器
            _timer.Dispose();
        }

        
    }
}
