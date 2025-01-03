using System;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Interstellar
{
    public static class File_operations
    {
        // Process all files in the directory passed in, recurse on any directories that are found, and process the files they contain.
        // Warning: I haven't fully tested 'recursive' here.
        public static void ProcessDirectory(string target_directory, string base_directory, List<Objects.files_in_directory> list, bool recursive)
        {
            // Process the list of files found in the directory.
            string [] fileEntries = Directory.GetFiles(target_directory);
            foreach(string fileName in fileEntries)
                ProcessFile(fileName, base_directory, list);

            // Recurse into subdirectories of this directory.
            if (recursive == true) {
                string [] subdirectoryEntries = Directory.GetDirectories(target_directory);
                foreach(string subdirectory in subdirectoryEntries)
                    ProcessDirectory(subdirectory, base_directory, list, true); // 'recursive' is always true here, as the original param was recursive. This would only be false in the future if there was a limit on recursiveness, e.g. only going 1 directory deep.
            }
        }

        // Insert logic for processing found files here.
        public static string ProcessFile(string file_path, string base_directory, List<Objects.files_in_directory> list)
        {
            string cropped_path = file_path.Replace(base_directory, "");
            if (cropped_path.StartsWith('/')) {
                cropped_path = cropped_path.TrimStart('/');
            }

            list.Add(new Objects.files_in_directory {
                file_name = Path.GetFileName(file_path),
                cropped_path = cropped_path,
                full_path = file_path
            });
            
            return file_path; // this probably doesn't need to be here.
        }

        public static List<Objects.files_in_directory> Get_files_in_directory(string[] base_paths, bool recursive)
        {
            List<Objects.files_in_directory> files_list = new List<Objects.files_in_directory>();
            foreach(string base_path in base_paths)
            {
                if (File.Exists(base_path))
                {
                    // This path is a file
                    File_operations.ProcessFile(base_path, base_path, files_list);
                }
                else if (Directory.Exists(base_path))
                {
                    // This path is a directory
                    File_operations.ProcessDirectory(base_path, base_path, files_list, recursive);
                }
                else
                {
                    Console.WriteLine("{0} is not a valid file or directory.", base_path);
                }
            }

            return files_list;
        }
    }
}