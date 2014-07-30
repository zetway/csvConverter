using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;


namespace csvConverter
{
    class ActionItem
    {
        public string Action;
        public int Position;
        public string Value;    
    }    
    
    class ConverterConfig
    {
        public ConverterFiles Files;
        public string CSVHeader;
        public int PopulateRowIfCellNumberIsEmpty;
        public ActionItem[] RearangeSequense;
    }

    struct ConverterFiles
    {
        public string Directory;
        public string[] Filenames;
        public string VariableName;
        public string FileExtension;
        public string OutputAppendix;
    }

    class Converter
    {
        public enum ConvertionResult
        {
            Success,
            ErrorReadingFile,
            ErrorWritingFile,
            ErrorParsing
        }

        private Dictionary<string, string> varStack = new Dictionary<string, string>();        
        private ConverterConfig config;
        private List<string> prevMainLine = new List<string>();
        
        private List<string> currentRow = new List<string>();        
        private bool canConvert = false;
        public Converter()
        {     
            
        }

        public void TryInstantiateConfig(string jsonConfig)
        {            
            config = JsonConvert.DeserializeObject<ConverterConfig>(jsonConfig);                       
            config.CSVHeader = config.CSVHeader.TrimEnd(new char[] { '\r', '\n', ' ' });
            
            canConvert = true;            
        }

        public ConvertionResult ConvertFiles()
        {
            if (!canConvert) return ConvertionResult.ErrorParsing;
            StringBuilder textBuilder;
            string inputText;
            string row = "";
            int inputIndex;
            foreach (string file in config.Files.Filenames)
            {
                
                inputIndex = 0;
                textBuilder = new StringBuilder();
                inputText = "";
                string inputFile = config.Files.Directory +
                    file +
                    config.Files.FileExtension;
                string currentFileVarName = config.Files.VariableName.Substring(1);
                varStack[currentFileVarName] = file;

                try
                {
                    using (StreamReader sr = new StreamReader(inputFile))
                    {
                        inputText = sr.ReadToEnd();                        
                        ReadRow(inputText, ref inputIndex, out row);
                        while (ReadRow(inputText, ref inputIndex, out row))
                        {
                            textBuilder.AppendLine(ConvertRow(row));
                        }
                    }
                }
                catch (Exception e)
                {
                    throw;
                }
                string outputFile = config.Files.Directory +
                    file +
                    config.Files.OutputAppendix +
                    config.Files.FileExtension;
                try
                {
                    using (StreamWriter sw = new StreamWriter(
                                new FileStream(outputFile, FileMode.Create)
                                ))
                    {
                        sw.Write(config.CSVHeader + "\r\n" + textBuilder.ToString());
                    }
                }
                catch (Exception)
                {

                    throw;
                }                
            }
            return ConvertionResult.Success;
        }

        private string ConvertRow(string line)
        {
            SplitToCells(line);
            setPrevLineOrPopulateLine();
            RunRearangeSquense();
            StringBuilder convertedLine = new StringBuilder();
            foreach (string cell in currentRow)
            {
                convertedLine.Append(cell + ",");
            }

            return convertedLine.ToString().TrimEnd(new char[] { ',' });
        }
        
        private string CompileValue(string value)
        {
            StringBuilder compiledValue = new StringBuilder(value);
            Regex varReg = new Regex(@"\{\$([a-z,A-Z][a-z,A-Z,0-1]*)\}");
            Regex cellNumReg = new Regex(@"\{\#(\d+)\}");
            Match var = varReg.Match(value);
            Match cell = cellNumReg.Match(value);
            while (var.Success)
            {
                string varName = var.Groups[0].Value;
                string varVal = var.Groups[1].Value;
                if (isVariableSet(varVal))
                    compiledValue.Replace(varName, varStack[varVal]);
                var = var.NextMatch();
            }
            while (cell.Success)
            {
                string cellNum = cell.Groups[0].Value;
                string celVal = cell.Groups[1].Value;
                int cellNumber = int.Parse(celVal);
                if (isCellNumberValid(cellNumber))
                    compiledValue.Replace(cellNum, currentRow[cellNumber]);
                cell = cell.NextMatch();
            }
            return compiledValue.ToString();
        }

        private bool isCellNumberValid(int number)
        {
            if (number < currentRow.Count && number >= 0) return true;
            return false;
        }

        private bool isVariableSet(string varName)
        {
            if (varStack.ContainsKey(varName)) return true;
            return false;
        }

        private bool ReadRow(string inputText, ref int index, out string row)
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
                
        private void RunRearangeSquense()
        {
            for (int i = 0; i < config.RearangeSequense.Length; i++)
            {
                RunAction(config.RearangeSequense[i]);
            }
        }

        private void RunAction(ActionItem action)
        {
            int position = action.Position;            
            string value = action.Value;
            if (position >= 0) currentRow[position] = currentRow[position].Trim(new char[] { '"' });
            switch (action.Action)
            {
                case "MarkRemove":
                    RunMarkRemove(position, value);
                    return;
                case "FetchRemove":
                    RunFetchRemove(position, value);
                    return;
                case "Insert":
                    RunInsert(position, value);
                    break;
                case "Pop":
                    RunPopCell(position, value);
                    return;
                case "Modify":
                    RunModify(position, value);
                    break;
                default:
                    return;
            }
            currentRow[position] = "\"" + currentRow[position] + "\"";
        }

        private void RunMarkRemove(int position, string value)
        {
            int count = Convert.ToInt32(value);
            for (int i = position; i < position + count; i++)
            {
                currentRow[i] = "!remove!";    
            }
            
        }
        private void RunFetchRemove(int position, string value)
        {
            currentRow.RemoveAll((arg) => { return arg == "!remove!"; });
        }
        private void RunInsert(int position, string value)
        {
            currentRow.Insert(position, CompileValue(value));
        }
        private void RunModify(int position, string value)
        {
            currentRow[position] = CompileValue(value);
        }
        private void RunPopCell(int position, string value)
        {
            value = value.Substring(1);
            varStack[value] = currentRow[position];
            currentRow.RemoveAt(position);
        }

        private void SplitToCells(string line)
        {            
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
                    currentRow.Add(line.Substring(start, count));
                    start = i + 1;
                    count = -1;
                }
                count++;
            }
            
        }

        private void CopyValuesFromPrevMainLine()
        {
            for (int i = 0; i < currentRow.Count; i++)
            {
                if (currentRow[i] == "" || currentRow[i] == "\"\"")
                    currentRow[i] = prevMainLine[i];
            }
        }

        private void setPrevLineOrPopulateLine()
        {
            int lookupIndex = config.PopulateRowIfCellNumberIsEmpty;
            if (lookupIndex < 0) return;
            if (prevMainLine == null)
            {
                prevMainLine = new List<string>();
                prevMainLine.AddRange(currentRow);
                return;
            }
            if (currentRow[lookupIndex] == "" ||
                currentRow[lookupIndex] == "\"\"")
            {
                CopyValuesFromPrevMainLine();
            }
            else
            {
                prevMainLine.Clear();
                prevMainLine.AddRange(currentRow);
            }
        }     

    }
}
