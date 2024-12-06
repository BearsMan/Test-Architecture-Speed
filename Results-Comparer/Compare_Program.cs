﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using System.Runtime.Serialization;

namespace Results_Comparer
{
    internal class Compare_Program
    {

        // Globals
        public static string x64FileName;
        public static string x86FileName;

        public static string x64FileName_Alt;
        public static string x86FileName_Alt;

        public static string x64Path;
        public static string x86Path;

        public static bool usingDebugFiles = false;

        [DataContract]
        private class Result
        {
            [DataMember]
            public string TestName { get; set; }
            [DataMember]
            public decimal TestResultValue { get; set; }
            [DataMember]
            public string TestResultUnit { get; set; }
            [DataMember]
            public int RunNumber { get; set; }
        }

        private class TestResult
        {
            public string TestName { get; set; }
            public decimal AverageResult { get; set; }
            public string ResultUnit { get; set; }
        }


        // ----------------- Main -----------------

        static void Main(string[] args)
        {
            // File names
            x64FileName = "Results64bit.json";
            x86FileName = "Results32bit.json";
            // The file names produced if the program was run in debug mode
            x64FileName_Alt = "Results64bit_debug.json";
            x86FileName_Alt = "Results32bit_debug.json";

            var pathsResult = DetermineFilePaths();

            if (pathsResult == null)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadLine();
                return;
            }
            else
            {
                (x64Path, x86Path) = pathsResult.Value;
            }

            // Deserialize the json files back into objects using the DataContractJsonSerializer
            List<Result> x64Results;
            List<Result> x86Results;

            using (FileStream fs = new FileStream(x64Path, FileMode.Open))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Result>));
                x64Results = (List<Result>)serializer.ReadObject(fs);
            }

            using (FileStream fs = new FileStream(x86Path, FileMode.Open))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Result>));
                x86Results = (List<Result>)serializer.ReadObject(fs);
            }

            // Compare the results
            CompareResults(x64Results, x86Results);

            Console.WriteLine("\n\nPress any key to exit");
            Console.ReadLine();

        }

        // -------------------------- Scaffolding / Results files detection functions --------------------------

        // Determine root path with the results files. Tries to see if the program is running from the solution directory, otherwise prompts user for folder
        static (string x64PathStr, string x86PathStr)? DetermineFilePaths()
        {
            //(string x64Path_local, string x86Path_local) SearchBothRegularAndDebugFileNames(string rootPath, bool searchSubDirectories)
            //{

            //}


            string rootPath;
            bool isUserEnteredPath = false; // We don't want to list all the files in the directory if the user entered the path since we don't know the size of the directory

            // First look in the current directory
            string _x64Path = SearchForFileInDirectory(rootPath: Directory.GetCurrentDirectory(), fileName: x64FileName, searchSubDirectories: false);
            string _x86Path = SearchForFileInDirectory(rootPath: Directory.GetCurrentDirectory(), fileName: x86FileName, searchSubDirectories: false);

            // If the files are not found in the current directory, try the debug file names
            if (_x64Path == null || _x86Path == null)
            {
                _x64Path = SearchForFileInDirectory(rootPath: Directory.GetCurrentDirectory(), fileName: x64FileName_Alt, searchSubDirectories: false);
                _x86Path = SearchForFileInDirectory(rootPath: Directory.GetCurrentDirectory(), fileName: x86FileName_Alt, searchSubDirectories: false);
                usingDebugFiles = true;
            }

            // If the files are still not found, check for the solution / repository structure
            if (_x64Path == null || _x86Path == null)
            {
                usingDebugFiles = false;

                var pathResult = DetermineProjectRootPath();
                if (pathResult == null) // This means a valid path was not auto-detected, AND the user did not enter a valid path, so we should exit
                {
                    return null;
                }
                else
                {
                    rootPath = pathResult.Value.rootPath;
                    isUserEnteredPath = pathResult.Value.userEnteredPath;
                }

                // Check hard coded paths first
                _x64Path = SearchForFileInDirectory(rootPath, x64FileName, searchSubDirectories: !isUserEnteredPath);
                _x86Path = SearchForFileInDirectory(rootPath, x86FileName, searchSubDirectories: !isUserEnteredPath);

                // If the files are not found in the root directory, try the debug file names
                if (_x64Path == null || _x86Path == null)
                {
                    _x64Path = SearchForFileInDirectory(rootPath, x64FileName_Alt, searchSubDirectories: !isUserEnteredPath);
                    _x86Path = SearchForFileInDirectory(rootPath, x86FileName_Alt, searchSubDirectories: !isUserEnteredPath);
                    usingDebugFiles = true;
                }
            }

            // If the files are still not found, if it was an auto-detected path, then prompt the user for the path, otherwise give up
            if (_x64Path == null || _x86Path == null)
            {
                usingDebugFiles = false;
                if (isUserEnteredPath)
                {
                    Console.WriteLine("Results files not found in the specified directory.");
                    return null;
                }
                // Prompt the user for the path
                else
                {
                    string userPath = PromptPath();
                    if (userPath == null)
                        return null;

                    _x64Path = SearchForFileInDirectory(userPath, x64FileName, searchSubDirectories: false);
                    _x86Path = SearchForFileInDirectory(userPath, x86FileName, searchSubDirectories: false);

                    // Check for debug file names
                    if (_x64Path == null || _x86Path == null)
                    {
                        _x64Path = SearchForFileInDirectory(userPath, x64FileName_Alt, searchSubDirectories: false);
                        _x86Path = SearchForFileInDirectory(userPath, x86FileName_Alt, searchSubDirectories: false);
                        usingDebugFiles = true;
                    }

                }
            }

            return (_x64Path, _x86Path);
        }

        static string SearchForFileInDirectory(string rootPath, string fileName, bool searchSubDirectories)
        {
            SearchOption option;
            if (searchSubDirectories)
            {
                option = SearchOption.AllDirectories;
            }
            else
            {
                option = SearchOption.TopDirectoryOnly;
            }

            string[] files = Directory.GetFiles(rootPath, fileName, option);
            if (files.Length == 0)
            {
                return null;
            }
            return files[0];
        }

        // Used to determine if the program is running from the solution directory / repository folder structure
        static (string rootPath, bool userEnteredPath)? DetermineProjectRootPath()
        {
            string projectRootPath;
            bool userEnteredPath = false;

            string currentDirectory = Directory.GetCurrentDirectory();
            // If "Results-Comparer" is in the path, then we are probably in the solution directory
            if (currentDirectory.Contains("Test-Architecture-Speed\\Results-Comparer"))
            {
                // Set the project root path to the solution directory (Test-Architecture-Speed)
                projectRootPath = currentDirectory.Substring(0, currentDirectory.IndexOf("Results-Comparer") - 1);

            }
            // Otherwise if "Results-Comparer" is at least in the path, use that as the root path
            else if (currentDirectory.Contains("Results-Comparer"))
            {
                projectRootPath = currentDirectory.Substring(0, currentDirectory.IndexOf("Results-Comparer") - 1);
            }
            else if (currentDirectory.Contains("Test-Architecture-Speed"))
            {
                projectRootPath = currentDirectory.Substring(0, currentDirectory.IndexOf("Test-Architecture-Speed") - 1);
            }
            else
            {
                // Prompt the user for the path to the results files
                projectRootPath = PromptPath();

                if (projectRootPath == null)
                    return null;

                userEnteredPath = true;
            }

            return (projectRootPath, userEnteredPath);
        }

        static string PromptPath()
        {
            Console.WriteLine("Enter the folder path containing the json results files.");
            Console.Write("Enter path: ");
            string input = Console.ReadLine();
            input = input.Trim('"');
            if (!Directory.Exists(input))
            {
                Console.WriteLine("Invalid Path - Path not found.");
                return null;
            }
            else
            {
                return input;
            }
        }

        // ------------------------------------ Actual comparison logic ------------------------------------
        static void CompareResults(List<Result> x64Results, List<Result> x86Results)
        {
            // Local function to get average results grouped by TestName
            List<TestResult> GetAverageResultsList(List<Result> results)
            {
                return results
                    .GroupBy(r => new { r.TestName, r.TestResultUnit })
                    .Select(g => new TestResult
                    {
                        TestName = g.Key.TestName,
                        AverageResult = g.Average(r => r.TestResultValue),
                        ResultUnit = g.Key.TestResultUnit
                    })
                    .ToList();
            }

            List<TestResult> x64TestResults = GetAverageResultsList(x64Results);
            List<TestResult> x86TestResults = GetAverageResultsList(x86Results);

            // Print file locations
            Console.WriteLine("x64 results file: {0}", x64Path);
            Console.WriteLine("x86 results file: {0}", x86Path);
            Console.WriteLine("\n\n");

            // Print table header
            Console.WriteLine("{0} | {1} | {2} | {3} | {4}",
                "Test Name".PadLeft((30 + 9) / 2).PadRight(30),    // (30 + 9) / 2 = 19.5 -> 19 spaces before
                "x64 Avg".PadLeft(8 + (16 - 8) / 2).PadRight(16),
                "x86 Avg".PadLeft(8 + (16 - 8) / 2).PadRight(16),
                "Winner".PadLeft(7 + (15 - 7) / 2).PadRight(15),
                "Difference".PadLeft(10 + (20 - 10) / 2).PadRight(20));

            Console.WriteLine("{0} | {1} | {2} | {3} | {4} | {5}",
                "".PadRight(30),
                "".PadRight(16),
                "".PadRight(16),
                "x64".PadLeft(3 + (6 - 3) / 2).PadRight(6),           // Center in 6 chars
                "x86".PadLeft(3 + (6 - 3) / 2).PadRight(6),           // Center in 6 chars
                "".PadRight(20));

            // Separator line remains the same
            Console.WriteLine("{0,-30} | {1,16} | {2,16} | {3,6} | {4,6} | {5,20}",
                new string('-', 30),
                new string('-', 16),
                new string('-', 16),
                new string('-', 6),
                new string('-', 6),
                new string('-', 20));

            // Compare the results
            for (int i = 0; i < x64TestResults.Count; i++)
            {
                TestResult x64TestResult = x64TestResults[i];
                TestResult x86TestResult = x86TestResults[i];

                if (x64TestResult.TestName != x86TestResult.TestName)
                {
                    Console.WriteLine("Test names do not match.");
                    return;
                }

                string x64Faster = "";
                string x86Faster = "";
                string difference = "";

                if (x64TestResult.AverageResult < x86TestResult.AverageResult)
                {
                    string diff = $"{Math.Round((x86TestResult.AverageResult - x64TestResult.AverageResult) / x86TestResult.AverageResult * 100, 2)}%";
                    x64Faster = "√";
                    difference = diff;
                }
                else if (x64TestResult.AverageResult > x86TestResult.AverageResult)
                {
                    string diff = $"{Math.Round((x64TestResult.AverageResult - x86TestResult.AverageResult) / x64TestResult.AverageResult * 100, 2)}%";
                    x86Faster = "√";
                    difference = diff;
                }
                else
                {
                    difference = "0%";
                }

                Console.WriteLine("{0,-30} | {1,10:F2} {2,-5} | {3,10:F2} {4,-5} | {5,6} | {6,6} | {7,-20}",
                    x64TestResult.TestName,
                    x64TestResult.AverageResult, x64TestResult.ResultUnit,
                    x86TestResult.AverageResult, x86TestResult.ResultUnit,
                    x64Faster.PadLeft(3).PadRight(6),
                    x86Faster.PadLeft(3).PadRight(6),
                    difference);
            }

        } // End of CompareResults

    } // ------------------ End of class Compare_Program ------------------

} // ------------------ End of namespace Results_Comparer ------------------
