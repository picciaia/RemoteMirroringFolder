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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RMF
{
    /// <summary>
    /// Utility class with a set of public static functions to deal with file and folder-related operations
    /// </summary>
    public class FileUtils
    {
        public static bool IsDir(string path)
        {
            return Path.GetExtension(path) == "";
        }
        public static bool IsInUse(string path)
        {
            if (IsDir(path))
            {
                return !IsFolderCopyDone(path);
            }
            else 
            {
                return IsFileInUse(path);
                
            }
        }

        public static bool IsFileInUse(string filename)
        {
            return IsFileInUse(new FileInfo(filename));
        }

        /// <summary>
        /// Returns TRUE if a file is currently in use
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static bool IsFileInUse(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            return false;
        }

        /// <summary>
        /// Returns TRUE when a copy-operation on a folder (thant contains both files and sub-folders) is complete
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        public static bool IsFolderCopyDone(string folderPath)
        {
            var copyDone = true;

            if (!Directory.Exists(folderPath))
                return true;

            foreach (var f in Directory.GetFiles(folderPath))
            {
                copyDone = copyDone && !IsFileInUse(f);
                if (!copyDone)
                    return false;
            }

            foreach (var d in Directory.GetDirectories(folderPath))
            {
                copyDone = copyDone && IsFolderCopyDone(d);
                if (!copyDone)
                    return false;
            }

            return true;
        }

        public static void Copy(string source, string dest)
        {
            if (IsDir(source))
            {
                CopyFolder(source, dest);
            }
            else
            {
                CopyFile(source, dest);
            }
        }

        public static bool CopyFile(string source, string dest)
        {
            File.Copy(source, dest);
            return true;
        }

        public static bool CopyFolder(string source, string dest, bool copySubDirs = true, bool waitForFileAvailable = true, int maxAttempts = 1000)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(source);

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(dest))
            {
                Directory.CreateDirectory(dest);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(dest, file.Name);
                try
                {
                    if (waitForFileAvailable)
                    {
                        int nAttempt = 0;
                        while (FileUtils.IsFileInUse(file) && nAttempt < maxAttempts)
                        {
                            Thread.Sleep(1000);
                            nAttempt++;
                        }
                    }

                    if (!FileUtils.IsFileInUse(file))
                        file.CopyTo(temppath, false);
                }
                catch (Exception ex)
                {
                    Log("FileCopy.CopyFolder error " + ex.ToString());
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(dest, subdir.Name);
                    try
                    {
                        CopyFolder(subdir.FullName, temppath, copySubDirs);
                    }
                    catch (Exception ex)
                    {
                        Log("FileCopy.CopyFolder subdir error " + ex.ToString());
                    }
                }
            }
            return false;
        }


        private static void Log(string format, params object[] arg)
        {
            Console.Write("[{0}] ", DateTime.Now);
            Console.WriteLine(format, arg); 
        }

    }
}
