using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace mHealthDataTasks
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.Msg("start process");
            string mHealthUrlTestDataRoot = "https://mhealthtechinterview.azurewebsites.net/api";
            string mHealthUrlTestDataEmployees = mHealthUrlTestDataRoot + "/" + "employees";
            string mHealthUrlTestDataDepartments = mHealthUrlTestDataRoot + "/" + "departments";
            string mHealthUrlTestDataToDos = mHealthUrlTestDataRoot + "/" + "todos";
            string strDataEmployees, strDataDepartments, strDataToDos;
            string strTestDataPath = "C:\\Temp\\JsonTest";
            var JsonSettings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore, MissingMemberHandling = MissingMemberHandling.Ignore};
            int intValidCount = 0, intNumExpectedLists = 3;
            const bool bTestMode = false;

            // here we fetch the online data if not in testmode, but also fetch
            // if testmode is requested AND the data doesn't yet exist locally for testing
            if (bTestMode==false || (bTestMode==true && IsTestDataAvail(strTestDataPath, "*.txt", 3)==false))
            {
                Log.Msg("fetch json data");
                HttpClient client = new HttpClient();
                strDataEmployees = FetchDataAsString(client, mHealthUrlTestDataEmployees).Result;
                strDataDepartments = FetchDataAsString(client, mHealthUrlTestDataDepartments).Result;
                strDataToDos = FetchDataAsString(client, mHealthUrlTestDataToDos).Result;
                if (bTestMode==true) 
                {
                    #pragma warning disable CS0162
                    Log.Msg("test mode");
                    Console.WriteLine("\nHere is the raw result for 'employees':\n" + strDataEmployees);
                    Console.WriteLine("\nHere is the raw result for 'departments':\n" + strDataDepartments);
                    Console.WriteLine("\nHere is the raw result for 'ToDos':\n" + strDataToDos);
                    Log.Msg("save data local copy");
                    SaveTestData(strTestDataPath + "\\Json-Employees.txt", strDataEmployees);
                    SaveTestData(strTestDataPath + "\\Json-Departments.txt", strDataDepartments);
                    SaveTestData(strTestDataPath + "\\Json-ToDos.txt", strDataToDos);
                    #pragma warning restore CS0162
                }
            }

            if (bTestMode == true) 
            {
                // load the data sets from a local copy, where we could inject values for testing
                #pragma warning disable CS0162
                Log.Msg("load local data");
                strDataEmployees = File.ReadAllText(strTestDataPath + "\\Json-Employees.txt").ToString();
                strDataDepartments = File.ReadAllText(strTestDataPath + "\\Json-Departments.txt").ToString();
                strDataToDos = File.ReadAllText(strTestDataPath + "\\Json-ToDos.txt").ToString();
                #pragma warning restore CS0162
            }

            TrimCleanDataString(ref strDataEmployees);
            TrimCleanDataString(ref strDataDepartments);
            TrimCleanDataString(ref strDataToDos);

            // some level of validation is needed, can't assume the data format is correct
            Log.Msg("validation");
            intValidCount += SimpleValidation(strDataEmployees);
            intValidCount += SimpleValidation(strDataDepartments);
            intValidCount += SimpleValidation(strDataToDos);

            if (intValidCount == intNumExpectedLists)
            {
                List<JsonSchemaForEmployees> jsonListEmployees = JsonConvert.DeserializeObject<List<JsonSchemaForEmployees>>(strDataEmployees, JsonSettings);
                List<JsonSchemaForDepartments> jsonListDepartments = JsonConvert.DeserializeObject<List<JsonSchemaForDepartments>>(strDataDepartments, JsonSettings);
                List<JsonSchemaForToDos> jsonListToDos = JsonConvert.DeserializeObject<List<JsonSchemaForToDos>>(strDataToDos, JsonSettings);

                if (bTestMode == true)
                {
                    #pragma warning disable CS0162
                    Log.Msg("raw data");
                    Console.WriteLine("\nList of employees, raw data:");
                    DisplayListEmployees(jsonListEmployees);
                    #pragma warning restore CS0162
                }

                Log.Msg("empl list no badge");
                Console.WriteLine("\n --------- Employees without Badges ---------");
                DisplayListOfEmployeesNoBadgeNumber(ref jsonListEmployees);

                Log.Msg("empl list by dept");
                PauseAndPromptUser("Press ENTER for next report");
                Console.WriteLine("\n --------- Employees by Department ---------");
                DisplayListOfEmployeesByDepartment(ref jsonListEmployees, ref jsonListDepartments);

                Log.Msg("empl list tasks");
                PauseAndPromptUser("Press ENTER for next report");
                Console.WriteLine("\n --------- Assigned Tasks by Due Date ---------");
                DisplayListOfEmployeeTasksByDueDate(ref jsonListEmployees, ref jsonListToDos);
            } else
            {
                Log.Msg("data format error");
                Console.WriteLine("\nError: source data format is not correct.");
            }

            Log.Msg("wait for user");
            PauseAndPromptUser("Press ENTER to quit");
            Log.Msg("end process");
        }

        // ======================================================================
        // This section contains methods for displaying data
        // ======================================================================
        static void DisplayListOfEmployeesNoBadgeNumber(ref List<JsonSchemaForEmployees> jsonListEmpl)
        {
            List<JsonSchemaForEmployees> noBadgeList = new List<JsonSchemaForEmployees>(jsonListEmpl);
            // may be an assumption for now that no badge# means a zero int value
            noBadgeList.RemoveAll(item => item.badgeNumber>0);
            DisplayListEmployees(noBadgeList, true);
        }

        static void DisplayListOfEmployeesByDepartment(ref List<JsonSchemaForEmployees> jsonListEmpl, ref List<JsonSchemaForDepartments> jsonListDept)
        {
            foreach (var dept in jsonListDept)
            {
                Console.WriteLine(dept.name);
                // traversing emply list ea time is not that efficient, poss. use jsonListEmpl.FindAll(something)
                foreach (var empl in jsonListEmpl)
                {
                    if (empl.departmentId==dept.Id)
                    {
                        Console.WriteLine("    " + empl.lastName + ", " + empl.firstName);
                    }
                }
            }
        }

        static void DisplayListOfEmployeeTasksByDueDate(ref List<JsonSchemaForEmployees> jsonListEmpl, ref List<JsonSchemaForToDos> jsonListToDos)
        {
            List<JsonSchemaForAssignedTasks> jsonListTasks = new List<JsonSchemaForAssignedTasks>();
            string strDueDatePrev = "";
            jsonListToDos.Sort((x, y) => y.DueDate.CompareTo(x.DueDate));
            foreach (var todo in jsonListToDos)
            {
                // we look for a date 'edge" detection, then output the 'previous' group
                if (!(strDueDatePrev.CompareTo(todo.DueDate.Substring(0, 10)) == 0))
                {
                    if (strDueDatePrev.Length > 0) Console.WriteLine(strDueDatePrev.Replace('-', '/'));
                    jsonListTasks.Sort((x, y) => x.lastName.CompareTo(y.lastName));
                    DisplayGroupOfTasks(jsonListTasks);
                    jsonListTasks.Clear();
                    strDueDatePrev = todo.DueDate.Substring(0, 10);
                }
                foreach (var empl in jsonListEmpl)
                {
                    if (empl.Id == todo.AssigneeId)
                    {
                        // build a new list, a group, so we can sort it before display
                        // the csharp list add method adds by ref by default, so create a new obj here
                        JsonSchemaForAssignedTasks jsonObjTasks = new JsonSchemaForAssignedTasks();
                        jsonObjTasks.dueDate = todo.DueDate.Substring(0, 10);
                        jsonObjTasks.taskDesc = todo.Description;
                        jsonObjTasks.lastName = empl.lastName;
                        jsonObjTasks.firstName = empl.firstName;
                        jsonListTasks.Add(jsonObjTasks);
                    }
                }
            }
        }

        static void DisplayGroupOfTasks(List<JsonSchemaForAssignedTasks> jsonList)
        {
            foreach (var item in jsonList)
            {
                Console.WriteLine("    " + item.lastName + ", " + item.firstName + " - " + "\"" + item.taskDesc + "\"");
            }
        }

        static void DisplayListEmployees(List<JsonSchemaForEmployees> jsonList, bool bAbbrev=false)
        {
            foreach (var item in jsonList)
            {
                if (bAbbrev==false) 
                    Console.WriteLine(item.Id + " - " + item.lastName + ", " + item.firstName + ", " + item.departmentId + ", " + item.position  + ", " + item.badgeNumber + ", " + item.hiredDate + ", " + item.productivityScore);
                else
                    Console.WriteLine(item.Id + " - " + item.lastName + ", " + item.firstName);
            }
        }

        // ======================================================================
        // This section contains a few helper methods
        // ======================================================================
        static int SimpleValidation(string strThisData)
        {
            string strRegExpr = @"\[\{.*:.*\}\]";
            Match m = Regex.Match(strThisData, strRegExpr, RegexOptions.IgnoreCase);
            return m.Success==true ? 1 : 0;
        }

        static void TrimCleanDataString(ref string strThis)
        {
            // using pass-by-ref for performance considerations
            strThis = strThis.Replace("\\", "");
            strThis = strThis.TrimStart('"');
            strThis = strThis.TrimEnd('"');
        }

        static void SaveTestData(string strPath, string strData)
        {
            File.WriteAllText(strPath, strData);
        }

        static bool IsTestDataAvail(string strPath, string strFileSpec, int intExpectedCount)
        {
            int intCount = Directory.GetFiles(strPath, strFileSpec, SearchOption.TopDirectoryOnly).Length;
            return (intCount == intExpectedCount);
        }

        static void PauseAndPromptUser(string strMsg)
        {
            Console.WriteLine("\n" + strMsg);
            Console.ReadKey();
        }
        static async Task<string> FetchDataAsString(HttpClient thisClient, string thisAddr)
        {
            string response = await thisClient.GetStringAsync(thisAddr);
            return response;
        }

        // ======================================================================
        // Schema/model definitions
        // ======================================================================
        class JsonSchemaForEmployees
        {
            public int Id { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
            public int departmentId { get; set; }
            public string position { get; set; }
            public int badgeNumber { get; set; }
            public string hiredDate { get; set; } // as string for now, for prototyping, and in case data is not pop. correctly
            public float productivityScore { get; set; }
        }

        class JsonSchemaForDepartments
        {
            public int Id { get; set; }
            public string name { get; set; }
        }

        class JsonSchemaForToDos
        {
            public int Id { get; set; }
            public int AssigneeId { get; set; }
            public string Description{ get; set; }
            public string DueDate{ get; set; } // as string for now, for prototyping, and in case data is not pop. correctly
        }

        class JsonSchemaForAssignedTasks
        {
            public string dueDate { get; set; }
            public string taskDesc { get; set; }
            public string firstName { get; set; }
            public string lastName { get; set; }
        }

        // ======================================================================
        // A simple run-time logging utility
        // ======================================================================
        public static class Log
        {
            static string strTempDir = System.Environment.GetEnvironmentVariable("Temp");
            static string thisProgramName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            static string logPath = strTempDir + "\\" + thisProgramName + ".log";
            public static void Msg(string text)
            {
                using (StreamWriter writer = new StreamWriter(logPath, true))
                {
                    writer.WriteLine($"{DateTime.Now} : {text}");
                }
            }
        }
    }
}
