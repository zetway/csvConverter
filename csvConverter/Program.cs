using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace csvConverter
{
    class Program
    {
        
        static void Main(string[] args)
        {            
            Main1();
            //PerformConvertion();
            
           
        }
        private static List<string> foldersList = new List<string>();
        private static void PerformConvertion()
        {
            string jsonConfig;
            using (StreamReader sr = new StreamReader(@"D:\config.js"))
            {
                jsonConfig = sr.ReadToEnd();
            }
            Converter converter = new Converter();
            try
            {
                converter.TryInstantiateConfig(jsonConfig);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                string resp = "";
                while (resp != "Y" && resp != "N")
                {
                    Console.WriteLine("Try Again? Y/N");
                    resp = Console.ReadLine().ToUpper();
                    if (resp == "Y")
                    {
                        PerformConvertion();
                    }                    
                }
                
            }
            Converter.ConvertionResult result = Converter.ConvertionResult.Success;
            try
            {
                result = converter.ConvertFiles();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
            
            //Converter.ConvertionResult result = Converter.ConvertionResult.Success;
            Console.WriteLine(result);
        }


        static List<string> prevMainLine;

        static string[] projects = new string[] { "Brady Online" };
        static int projIndex = 0;

        

        private static void SaveSerializedConfig(ConverterConfig config, string filename)
        {
            string res = JsonConvert.SerializeObject(config);
            using (StreamWriter sw = new StreamWriter(filename))
            {
                sw.Write(res);
            }
        }

        private static void Main1()
        {
            string INPUT_ENDING = ".csv";
            string OUTPUT_ENDING = "_out.csv";

            string header = "Folder Name,Title,Status,Description,Execution Type,Step_Seq,Step_Critical,Step_Steps,Step_Expected Result\r\n";
            StringBuilder textBuilder;
            string inputText;
            string row = "";
            int inputIndex;
            for (projIndex = 0; projIndex < projects.Length; projIndex++)
            {
                inputIndex = 0;
                textBuilder = new StringBuilder();
                inputText = "";
                using (StreamReader sr = new StreamReader(@"D:\" + projects[projIndex] + INPUT_ENDING))
                {
                    inputText = sr.ReadToEnd();
                    ReadRow(inputText, ref inputIndex, out row);
                    while (ReadRow(inputText, ref inputIndex, out row))
                    {
                        textBuilder.AppendLine(ConvertRow(row));
                    }
                }
                using (StreamWriter sw = new StreamWriter(new FileStream(@"D:\" + projects[projIndex] + OUTPUT_ENDING, FileMode.Create)))
                {
                    sw.Write(header + textBuilder.ToString());
                }
                foldersList.Sort();
                StringBuilder foldersBuilder = new StringBuilder();
                foreach (string folder in foldersList)
                {
                    foldersBuilder.AppendLine(folder);
                }
                using (StreamWriter sw = new StreamWriter(new FileStream(@"D:\folders.txt", FileMode.Create)))
                {
                    sw.Write(foldersBuilder.ToString());
                }
                Console.WriteLine(projects[projIndex]);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputText"></param>
        /// <param name="index"></param>
        /// <param name="row"></param>
        /// <returns>true if succefully read a row</returns>
        static bool ReadRow(string inputText, ref int index, out string row)
        {
            bool isInsideQuotes = false;            
            int start = index;
            int count = 0;
            while (index < inputText.Length)
            {
                if (inputText[index] == '"')
                {
                    isInsideQuotes = !isInsideQuotes;
                }
                if ((inputText[index] == '\n' && !isInsideQuotes) || index == inputText.Length - 1)
                {
                    index++;
                    row = inputText.Substring(start, count);
                    return true;
                }
                index++;
                count++;
            }
            row = "";
            return false;
        }

        static string ConvertRow(string line)
        {
            List<string> cells = SplitToCells(line);
            DeleteOddCells(ref cells);
            setPrevLineOrPopulateLine(ref cells);
            RearangeCells(ref cells);
            StringBuilder convertedLine = new StringBuilder();
            foreach (string cell in cells)
            {
                convertedLine.Append(cell + ",");
            }

            return convertedLine.ToString().TrimEnd(new char[] { ',' });
        }

        static List<string> SplitToCells(string line)
        {
            List<string> cells = new List<string>();
            int start = 0;
            int count = 0;
            bool isInsideTheQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    isInsideTheQuotes = !isInsideTheQuotes;
                }                
                if ((line[i] == ',' || i == line.Length - 1) && !isInsideTheQuotes)
                {
                    cells.Add(line.Substring(start, count));
                    start = i + 1;
                    count = -1;
                }
                count++;
            }
            return cells;
        }

        static void CopyValuesFromPrevMainLine(ref List<string> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == "" || cells[i] == "\"\"")
                    cells[i] = prevMainLine[i];
            }
        }

        static void setPrevLineOrPopulateLine(ref List<string> cells)
        {
            if (prevMainLine == null)
            {
                prevMainLine = new List<string>();
                prevMainLine.AddRange(cells);
                return;
            }
            if (cells[0] == "" || cells[0] == "\"\"")
            {
                CopyValuesFromPrevMainLine(ref cells);
            }
            else
            {
                prevMainLine.Clear();
                prevMainLine.AddRange(cells);
            }
        }

        static void RearangeCells(ref List<string> cells)
        {
            string folderCell = PopCell(ref cells, 1).Trim(new char[] { '"' }).Replace("|", "/");
            if (!foldersList.Contains(folderCell)) foldersList.Add(folderCell);
            cells.Insert(0, folderCell);
            cells.Insert(2, "Approved");            
            string objectiveCell = PopCell(ref cells, 4).Trim(new char[] { '"' });
            
            cells.Insert(4, "Manual");
            string preRequisitesCell = PopCell(ref cells, 5).Trim(new char[] {'"'});

            cells[3] = "\"" + 
                cells[3].Trim(new char[] { '"' }) + 
                "\r\nObjective:\r\n" + 
                objectiveCell + 
                "\r\nPrerequisites:\r\n" +
                preRequisitesCell +
                "\"";
            cells.Insert(6, "N");
            string stepsDataCell = PopCell(ref cells, 8).Trim(new char[] {'"'});
            string stepNotesCell = PopCell(ref cells, 9).Trim(new char[] {'"'});
            cells[7] = cells[7].Trim(new char[] { '"' });
            cells[7] = "\"Step Data:\r\n" + 
                stepsDataCell + 
                "\r\nStep Description:\r\n" + 
                cells[7] + 
                "\r\nStep Notes: \r\n" +
                stepNotesCell +
                "\"";
            cells[8] = "\"" + cells[8].Trim(new char[] { '"' }) + "\"";
        }

        static void DeleteOddCells(ref List<string> cells)
        {
            MarkRemovedCells(ref cells, 0, 2);
            MarkRemovedCells(ref cells, 4, 8);
            MarkRemovedCells(ref cells, 15, 2);
            FetchCells(ref cells);
        }

        static string PopCell(ref List<string> cells, int initCellIndex)
        {
            string cell = cells[initCellIndex];
            cells.RemoveAt(initCellIndex);
            return cell;
        }

        static void MarkRemovedCells(ref List<string> cells, int start, int count)
        {
            for (int i = 0; i < count; i++)
            {
                cells[start + i] = "!remove!";
            }
        }

        static void FetchCells(ref List<string> cells)
        {
            cells.RemoveAll((arg) => { return arg == "!remove!"; });
        }
    }
}
