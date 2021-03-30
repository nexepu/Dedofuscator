using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Dedofuscator
{
    internal record AsClass
    {
        public string OldName;
        public string NewName;
        public string FilePath;
        public string[] FileData;
        public List<AsVariable> Variables = new List<AsVariable>();
        public List<AsFunction> Functions = new List<AsFunction>();

        public AsClass(string oldName, string newName, string path, string[] fileData)
        {
            OldName = oldName;
            NewName = newName;
            FilePath = path;
            FileData = fileData;
        }
    }

    internal record AsVariable
    {
        public bool IsConstant;
        public string OldName;
        public string NewName;
        public string ReturnType;
        public string Value;

        public AsVariable(bool isConstant, string oldName, string newName, string returnType, string value)
        {
            IsConstant = isConstant;
            OldName = oldName;
            NewName = newName;
            ReturnType = returnType;
            Value = value;
        }
    }

    internal record AsFunction
    {
        public bool IsPrivate;
        public string OldName;
        public string NewName;
        public string ReturnType;
        public string ParametersRaw;
        public int BeginningBracketIndex;
        public string[] Code;

        public AsFunction(bool isPrivate, string oldName, string newName, string returnType)
        {
            IsPrivate = isPrivate;
            OldName = oldName;
            NewName = newName;
            ReturnType = returnType;
        }
        public AsFunction(bool isPrivate, string oldName, string newName, string returnType, string parametersRaw, int beginningBracketIndex) : this(isPrivate, oldName, newName, returnType)
        {
            BeginningBracketIndex = beginningBracketIndex;
            ParametersRaw = parametersRaw;
        }
    }

    internal record HumanCheckClass : AsClass
    {
        public string DotNetCode;
        public dynamic FunctionsClass;
        public HumanCheckClass(string oldName, string newName, string path, string[] fileData) : base(oldName, newName,
            path, fileData)
        {

        }

    }
    class Program
    {

        private static Regex WeirdClassNameRegex = new Regex(@"public class (?<name>§.*§)");
        private static Regex WeirdVariableNameRegex = new Regex(@"(?<accesibility>public|private) (?<static>static |)(?<type>const|var) (?<name>§.*§):(?<variableType>[^ ]+)(?<hasValue> = |;)(?<value>[^;]+|)");
        private static Regex WeirdFunctionNameRegex = new Regex(@"(?<accesibility>public|private) (?<static>static |)function (?<name>§.*§)\(.*\) : (?<returnType>.*)");

        //HumanCheck regex
        private static Regex ImportsRegex = new Regex(@"import (?<import>[^ ]+);");
        private static Regex HumanCheckVariableRegex = new Regex(@"(?<accesibility>public|private) (?<static>static |)(?<type>const|var) (?<name>_[A-Z]+):(?<variableType>[^ ]+)(?<hasValue> = |;)(?<value>[^;]+|)");
        private static Regex HumanCheckFunctionNameRegex = new Regex(@"(?<accesibility>public|private) (?<static>static |)function (?<name>_[A-Z]+)\((?<parameters>.*)\) : (?<returnType>.*)");
        private static Regex InFunctionVariableRegex = new Regex(@"(?<type>const|var) (?<name>.*):(?<variableType>[^ ]+)(?<hasValue> = |;)(?<value>[^;]+|)");
        private static Regex InFunctionParamRegex = new Regex(@"(?<name>[^ ]+):(?<variableType>[^ ]+)(?<hasValue> = |)(?<value>[^ ]+|)");

        private static string[] ImportsToInject = new[]
        {
            "import com.hurlant.crypto.*;",
            "import com.hurlant.crypto.cert.*;",
            "import com.hurlant.crypto.hash.*;",
            "import com.hurlant.crypto.prng.*;",
            "import com.hurlant.crypto.rsa.*;",
            "import com.hurlant.crypto.symmetric.*;",
            "import com.hurlant.crypto.tls.*;",
            "import com.hurlant.math.*;",
            "import com.hurlant.util.*;",
            "import com.hurlant.util.der.*;"
        };

        private static List<AsClass> WeirdClasses = new List<AsClass>();
        private static HumanCheckClass HumanCheckClass;

        private static string[] files;
        static void Main(string[] args)
        {
            Console.WriteLine("Loading files from path /Input"); 
            files = Directory.EnumerateFiles("./Input").Reverse().ToArray();
            ReadWeirdWeirdClasses();
            ReadVariablesInWeirdWeirdClasses();
            ReadHumanCheck();

            ReplaceWeirdNamesInWeirdClasses();
            Save();
        }

        static void ReadWeirdWeirdClasses()
        {
            var weirdNamesFiles = files.Where(x => x.Contains('§') && x.EndsWith(".as")).ToArray();
            int classCounter = 0;
            foreach (var weirdFileName in weirdNamesFiles)
            {
                var lines = File.ReadAllLines(weirdFileName);
                foreach (var line in lines)
                {

                    var match = WeirdClassNameRegex.Match(line);
                    if (match.Success)
                    {
                        var className = match.Groups["name"].Value;
                        Console.WriteLine($"Found class name in file {weirdFileName} : {className}");
                        if (WeirdClasses.Any(x => x.OldName == className))
                            throw new Exception("found two WeirdClasses with same name, was there an update to the dofuscator?");
                        WeirdClasses.Add(new AsClass(className, $"class_{classCounter++}", weirdFileName, lines));
                        break;
                    }
                }
            }
        }

        static void ReadHumanCheck()
        {
            var humanCheckFilePath = files.First(x => x.Contains("HumanCheck.as"));
            var humanCheckFileData = File.ReadAllLines(humanCheckFilePath);
            HumanCheckClass = new HumanCheckClass("HumanCheck", null, humanCheckFilePath, humanCheckFileData);
            var hcString = string.Join("\n", humanCheckFileData);

            ReplaceInArray(ref HumanCheckClass.FileData, "§*§", "*");  //we fix the imports

            var classNameIndex = Array.IndexOf(HumanCheckClass.FileData,
                HumanCheckClass.FileData.First(x => x.Contains("public class HumanCheck")));
            int beginningBracketIndex = 0;
            int endingBracketIndex = 0; 

            FindBracketsPosition(ref beginningBracketIndex, ref endingBracketIndex, HumanCheckClass.FileData, classNameIndex + 1);

          var realClass = HumanCheckClass.FileData.ToList().GetRange(0, endingBracketIndex + 2);
            //we inject the new imports of the clean hurlant library
            var tempList = realClass.Take(2).ToList();
            tempList.AddRange(ImportsToInject.Select(import => $"   {import}"));
            realClass.RemoveRange(0, 2);
            tempList.AddRange(realClass);
            realClass = tempList;
            //
            int varCounter = 0;
            int funcCounter = 0;
            for (int i = 0; i < realClass.Count; i++)
            {
                var line = realClass[i];
                var m = HumanCheckVariableRegex.Match(line);
                if (m.Success)
                {
                    Console.WriteLine($"[HumanCheck] found variable {m.Groups["name"].Value} with type {m.Groups["variableType"].Value} at line {i}");
                    HumanCheckClass.Variables.Add(new AsVariable(m.Groups["type"].Value == "const", m.Groups["name"].Value, $"var_{varCounter++}", m.Groups["variableType"].Value, null));
                }
                var m2 = HumanCheckFunctionNameRegex.Match(line);
                if (m2.Success)
                {
                    Console.WriteLine($"[HumanCheck] found function {m2.Groups["name"].Value} with return type {m2.Groups["returnType"].Value} at line {i}");
                    HumanCheckClass.Functions.Add(new AsFunction(m2.Groups["accesibility"].Value == "private", m2.Groups["name"].Value, $"func_{funcCounter++}", m2.Groups["returnType"].Value, m2.Groups["parameters"].Value, i + 1));
                    //int startingFuncBracketIndex=0;
                    //int endingFuncBracketIndex=0;
                    //FindBracketsPosition(ref startingFuncBracketIndex, ref endingFuncBracketIndex, realClass.ToArray(), i);
                    //var code = realClass.GetRange(i,  endingFuncBracketIndex - i + 1);

                }
            }
            //we replace the variables & functions names
            foreach (var @class in WeirdClasses)
            {
                ReplaceInArray(ref realClass, @class.OldName, @class.NewName);

                foreach (var variable in @class.Variables)
                {
                    ReplaceInArray(ref realClass, @class.NewName + "." + variable.OldName,
                        @class.NewName + "." + variable.NewName);
                }
                foreach (var function in @class.Functions)
                {
                    ReplaceInArray(ref realClass, @class.NewName + "." + function.OldName,
                        @class.NewName + "." + function.NewName);
                }
            }

            HumanCheckClass.Variables.ForEach(x => ReplaceInArray(ref realClass, x.OldName, x.NewName));
            HumanCheckClass.Functions.ForEach(x => ReplaceInArray(ref realClass, x.OldName, x.NewName));
            // we get each function's code with the updated names
            foreach (var function in HumanCheckClass.Functions)
            {
                int startingFuncBracketIndex = 0;
                int endingFuncBracketIndex = 0;
                FindBracketsPosition(ref startingFuncBracketIndex, ref endingFuncBracketIndex, realClass.ToArray(), function.BeginningBracketIndex);
                var code = realClass.GetRange(function.BeginningBracketIndex - 1, (endingFuncBracketIndex - function.BeginningBracketIndex) + 2).ToArray();
                function.Code = code;
            }
            //
            HumanCheckClass.FileData = realClass.ToArray();
            CreateDotNetClassFromAS();
            HumanCheckClass.FunctionsClass = CompilationHelper.CreateFunctionsClass(HumanCheckClass.DotNetCode);

        }

        static void CreateDotNetClassFromAS()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("public class Functions");
            sb.AppendLine("{");
            foreach (var variable in HumanCheckClass.Variables)
                sb.AppendLine($"\tpublic {variable.ReturnType} {variable.NewName};");
            sb.AppendLine();
            foreach (var function in HumanCheckClass.Functions)
            {
                List<string> newParams = new List<string>();
                var rawParams = function.ParametersRaw;
                var split = rawParams.Split(", ");
                foreach (var vr in split)
                {
                    var match = InFunctionParamRegex.Match(vr);
                    if (match.Success)
                    {
                        var strToAdd = match.Groups["hasValue"].Value == " = " ? $" = {match.Groups["value"].Value}" : "";
                        newParams.Add($"{match.Groups["variableType"].Value} {match.Groups["name"]}" + strToAdd);
                    }
                }
                sb.AppendLine($"\tpublic {function.ReturnType} {function.NewName}({string.Join(',', newParams)})");
                
                for(int i = 1; i < function.Code.Length; i++)
                {
                    var line = function.Code[i];
                    var m = InFunctionVariableRegex.Match(line);
                    if (m.Success)
                    {
                        var strToAdd = m.Groups["hasValue"].Value == " = " ? $" = {m.Groups["value"].Value};" : ";";
                        sb.AppendLine($"\t\t{m.Groups["variableType"].Value} {m.Groups["name"].Value}" + strToAdd);
                    }
                    else
                        sb.AppendLine(line);
                }
            }

            sb.AppendLine("}");
            HumanCheckClass.DotNetCode = sb.ToString();
            Console.WriteLine("[HumanCheck] Converted AS3 functions to C#");

        }
        static void ReadVariablesInWeirdWeirdClasses()
        {
            foreach (var @class in WeirdClasses)
            {
                int varCounter = 0;
                int funcCounter = 0;
                var data = string.Join('\n', @class.FileData);


                var varMatches = WeirdVariableNameRegex.Matches(string.Join('\n', data));
                foreach (var matchVariable in varMatches)
                {
                    if (matchVariable is Match m)
                    {
                        bool hasValue = m.Groups["hasValue"].Value == " = ";
                        string value = hasValue ? m.Groups["value"].Value : null;
                        if (m.Groups["type"].Value == "const")
                        {
                            if (hasValue)
                                Console.WriteLine($"[Class {@class.OldName}] Found constant {m.Groups["name"].Value} of type {m.Groups["variableType"].Value} ------> value = {value}");
                            else
                                Console.WriteLine($"[Class {@class.OldName}] Found constant {m.Groups["name"].Value} of type {m.Groups["variableType"].Value}");

                            @class.Variables.Add(new AsVariable(true,
                                m.Groups["name"].Value,
                                $"var_{varCounter++}",
                                m.Groups["variableType"].Value,
                                value));
                        }
                        else
                        {
                            if (hasValue)
                                Console.WriteLine($"[Class {@class.OldName}] Found variable {m.Groups["name"].Value} of type {m.Groups["variableType"].Value} ------> value = {value}");
                            else
                                Console.WriteLine($"[Class {@class.OldName}] Found variable {m.Groups["name"].Value} of type {m.Groups["variableType"].Value}");

                            @class.Variables.Add(new AsVariable(false,
                                m.Groups["name"].Value,
                                $"var_{varCounter++}",
                                m.Groups["variableType"].Value,
                                value));

                        }
                    }
                }

                var funcMatches = WeirdFunctionNameRegex.Matches(data);
                foreach (var match in funcMatches)
                {
                    if (match is Match m)
                    {
                        Console.WriteLine(
                                $"[Class {@class.OldName}] Found function {m.Groups["name"].Value} with return type {m.Groups["returnType"].Value}");
                        @class.Functions.Add(new AsFunction(m.Groups["accesibility"].Value == "private", m.Groups["name"].Value, $"func_{funcCounter++}", m.Groups["returnType"].Value));

                    }
                }

            }
        }

        static void ReplaceWeirdNamesInWeirdClasses()
        {
            var processedWeirdClasses = new List<string>();
            foreach (var @class in WeirdClasses)
            {

                ReplaceInArray(ref @class.FileData, @class.OldName, @class.NewName);

                @class.Variables.ForEach(x => ReplaceInArray(ref @class.FileData, x.OldName, x.NewName));
                @class.Functions.ForEach(x => ReplaceInArray(ref @class.FileData, x.OldName, x.NewName));

                var dependencies = WeirdClasses.Where(x =>
                    x.OldName != @class.OldName && x.FileData.Any(x => x.Contains(@class.OldName))).ToArray();
                foreach (var dependency in dependencies)
                {

                    ReplaceInArray(ref dependency.FileData, @class.OldName, @class.NewName);

                    if (dependency.FileData.Any(x => // check if a dependency uses this class's variables
                        @class.Variables.Any(vr =>x.Contains(@class.NewName + "." + vr.OldName)) ||
                        @class.Functions.Any(func => x.Contains(@class.NewName + "." + func.OldName))))
                    {
                        foreach (var variable in @class.Variables)
                        {
                            ReplaceInArray(ref dependency.FileData, @class.NewName + "." + variable.OldName,
                                @class.NewName + "." + variable.NewName);
                        }
                        foreach (var function in @class.Functions)
                        {
                            ReplaceInArray(ref dependency.FileData, @class.NewName + "." + function.OldName,
                                @class.NewName + "." + function.NewName);
                        }
                    }
                    //if (dependency.Variables.Any(x => x.ReturnType == "Class" && x.Value == @class.OldName))
                    //{
                    //    ReplaceInArray(dependency.FileData, @class.OldName, @class.NewName);
                    //}

                }
            }
        }

        static void Save()
        {
            if (!Directory.Exists("./Output/"))
                Directory.CreateDirectory("./Output/");
            foreach (var @class in WeirdClasses)
                File.WriteAllLines($"./Output/{@class.NewName}.as", @class.FileData);
            File.WriteAllLines($"./Output/{HumanCheckClass.OldName}.as", HumanCheckClass.FileData);
            Console.WriteLine("Saved!");
        }

        static void FindBracketsPosition(ref int startingBracket, ref int endingBracket, string[] arr, int startingIndex = -1)
        {
            List<int> openedBrackets = new List<int>();
            for (int i = startingIndex; i < arr.Length; i++) //we need to find the class's beginning brackets and ending ones
            {
                var currentLine = arr[i];
                var hasStartingBracket = Regex.Matches(currentLine, @"{");
                var hasEndingBrackets = Regex.Matches(currentLine, @"}");

                if (hasStartingBracket.Any())
                {
                    if (startingBracket == 0)
                    {
                        Console.WriteLine($"Found starting bracket of class at index {i}");
                        startingBracket = i;
                        continue;
                    }

                    for (int y = 0; y <= hasEndingBrackets.Count; y++)
                        openedBrackets.Add(i);
                }
                else if (hasEndingBrackets.Any())
                {
                    if (openedBrackets.Count != 0)
                        for (int z = 0; z < hasEndingBrackets.Count; z++)
                            openedBrackets.RemoveAt(0);
                    else
                    {
                        Console.WriteLine($"Found ending bracket of class at index {i}");
                        endingBracket = i;
                        break;
                    }
                }

            }
        }
        static void ReplaceInArray(ref string[] arr, string toReplace, string replacement)
        {
            arr = arr.Select(x => x.Replace(toReplace, replacement)).ToArray();
        }
        static void ReplaceInArray(ref List<string> arr, string toReplace, string replacement)
        {
            arr = arr.Select(x => x.Replace(toReplace, replacement)).ToList();
        }
    }
}
