using System;
using NCalc;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using System.Text.RegularExpressions;
using System.ComponentModel.Design;
using Windows.UI.Popups;



namespace prgToMPR02
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add(".prg"); // Select only PRG files

            var folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                // Print out Dir
                TextBlock_Dir.Text +=  folder.Path;

                // Find all PRG files in the selected directory
                IReadOnlyList<StorageFile> prgFiles = await folder.GetFilesAsync();
                List<string> dosyaIcerikleri = new List<string>();

                foreach (StorageFile file in prgFiles)
                {
                    try
                    {
                        string dosyaIcerigi = await FileIO.ReadTextAsync(file);
                        dosyaIcerikleri.Add(dosyaIcerigi);

                        // You can start processing the content of each file here.
                        // For example, you can analyze the commands in the file.
                    }
                    catch (Exception ex)
                    {
                        var dialog = new MessageDialog("File read error (" + file.Name + "): " + ex.Message);
                        await dialog.ShowAsync();
                    }
                }

                // After processing the file contents, you can perform the necessary conversion tasks.
                await ConvertAndSave(folder, dosyaIcerikleri, prgFiles);
            }
        }

        private async Task ConvertAndSave(StorageFolder folder, List<string> dosyaIcerikleri, IReadOnlyList<StorageFile> prgFiles)
        {
            for (int i = 0; i < dosyaIcerikleri.Count; i++)
            {
                string dosyaIcerigi = dosyaIcerikleri[i];
                StorageFile prgFile = prgFiles[i];
                string dosyaAdi = prgFile.DisplayName;

                // Conversion tasks are performed here.
                // The content of each file is found in the "dosyaIcerigi" variable.
                string mprIcerik = ConvertPRGtoMPR(dosyaIcerigi, dosyaAdi);

                // Dosya içeriğini TextBox içine ekleyin
                TextBoxOutput.Text += $"Folder : {dosyaAdi}\n";
                //TextBoxOutput.Text += $"{dosyaIcerigi}\n\n";

                if (!string.IsNullOrEmpty(mprIcerik))
                {
                    // You can retrieve the name of the converted PRG file.

                    await SaveMPRFile(dosyaAdi, mprIcerik, folder); // Save the MPR file
                }
            }
        }

        private string ConvertPRGtoMPR(string prgIcerik, string dosyaAdi)
        {
            List<string> mprLines = new List<string>();
            Dictionary<string, double> vars = new Dictionary<string, double>();
            Dictionary<string, string> frzElementsList = new Dictionary<string, string>();

            Dictionary<string, string> frzToolsList = new Dictionary<string, string>
            {
                { "T2", "15" },
                { "T3", "4" },
                { "T5", "5" },
                { "T6", "5" },
                { "T9", "8" },
                { "T35", "8" },
                { "T36", "6" },
                { "T37", "8" },
                { "T38", "6" }
            };

            frzElementsList["x"] = "";
            frzElementsList["y"] = "";
            frzElementsList["z"] = "";

            string[] lines = prgIcerik.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inG100Block = false;   // PROGRAMMING OF TOP HOLES
            bool inG101Block = false;   // PROGRAMMING OF VERTICAL HOLES
            bool inG172Block = false;   // SELECTION OF THE START POINT IN FIXED CYCLES +TOOL, ROTATION SPEED, ENTRY SPEED, ROUTING BIT INCLINATIONS 
            bool inG173Block = false;   // INTERPOLATION ORIGIN INDIPENDENT FROM WORKING FACE
            bool inG182Block = false;   // HORIZONTAL HOLES IN WORKPIECE LEFT FACE
            bool inG183Block = false;   // HORIZONTAL HOLES IN WORKPIECE RIGHT FACE
            bool inG184Block = false;   // HORIZONTAL HOLES IN WORKPIECE REAR FACE
            bool inG185Block = false;   // HORIZONTAL HOLES IN WORKPIECE FRONT FACE

            bool inWhileLoop = false;   // HORIZONTAL HOLES IN WORKPIECE FRONT FACE
            int whileLoopIndexStart = 0;
            int whileLoopIndexEnd = 0;
            string whileCond = "false";

            bool firstPoint = false;   // INTERPOLATION ORIGIN INDIPENDENT FROM WORKING FACE
            int frzCnt = 0;
            int frzOpCnt = 0;

            string en, enf3, boy, boyf3, th, thf3;
            en = "";
            th = "";
            boy = "";
            enf3 = "";
            thf3 = "";
            boyf3 = "";

            // Add the MPR header section
            mprLines.Add("[H");
            mprLines.Add("VERSION=\"7.0\"");
            mprLines.Add("MAT=\"HOMAG\"");
            mprLines.Add("OP=\"1\"");
            mprLines.Add("O2=\"0\"");
            mprLines.Add("O3=\"0\"");
            mprLines.Add("O4=\"0\"");
            mprLines.Add("O5=\"0\"");
            mprLines.Add("FM=\"1\"");
            mprLines.Add("CB=\"0\"");
            mprLines.Add("ML=\"2000\"");
            mprLines.Add("GP=\"0\"");
            mprLines.Add("GY=\"0\"");
            mprLines.Add("GXY=\"0\"");
            mprLines.Add("NP=\"1\"");
            mprLines.Add("NE=\"0\"");
            mprLines.Add("NA=\"0\"");
            mprLines.Add("UP=\"0\"");
            mprLines.Add("DW=\"0\"");
            mprLines.Add("INCH=\"0\"");
            mprLines.Add("VIEW=\"NOMIRROR\"");

            // Start processing the PRG content


            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]; // delete the row

                string trimmedLine = line.Trim();

                if ((trimmedLine.StartsWith("G100") || trimmedLine.StartsWith("G172") || trimmedLine.StartsWith("G173") || trimmedLine.StartsWith("G182") || trimmedLine.StartsWith("G183") || trimmedLine.StartsWith("%")) && inG172Block)
                {
                    inG172Block = false;

                    string e1 = "1";
                    string depth = "-3";
                    if (frzOpCnt > 2) e1 = "2";
                    if (frzElementsList["z"] != "") depth = "@-" + frzElementsList["z"];

                    mprLines.Add(".WI=1.571");
                    mprLines.Add(".WZ=0.000");
                    mprLines.Add("");

                    mprLines.Add("<105 \\Freze\\");
                    mprLines.Add("EA=\"" + e1 + ":0\"");
                    mprLines.Add("MDA=\"TAN\"");
                    mprLines.Add("RK=\"WRKR\"");
                    mprLines.Add("EE=\"" + e1 + ":" + Convert.ToString(frzOpCnt - 1) + "\"");
                    mprLines.Add("MDE=\"TAN_AB\"");
                    mprLines.Add("EM=\"0\"");
                    mprLines.Add("RI=\"1\"");
                    mprLines.Add("TNO=\"101\"");
                    mprLines.Add("SM=\"0\"");
                    mprLines.Add("S_=\"STANDARD\"");
                    mprLines.Add("F_=\"STANDARD\"");
                    mprLines.Add("AB=\"0\"");
                    mprLines.Add("ZA=\"" + depth + "\"");
                    mprLines.Add("KM=\"simple mill\"");
                    mprLines.Add("KM=\"8.5\"");
                    mprLines.Add("KM=\"\"");
                    mprLines.Add("");

                }

                if (trimmedLine.StartsWith("G172"))
                {
                    if (!inG172Block) inG172Block = true;
                    if (!firstPoint) firstPoint = true;

                    frzOpCnt = 0;
                    frzCnt++;
                    mprLines.Add("]" + frzCnt);
                }

                if (trimmedLine.StartsWith("|4 "))
                {
                    en = trimmedLine.Split(new string[] { "|4 " }, StringSplitOptions.None)[1];
                }
                else if (trimmedLine.StartsWith("|5 "))
                {
                    th = trimmedLine.Split(new string[] { "|5 " }, StringSplitOptions.None)[1];
                }
                else if (trimmedLine.StartsWith("|6 "))
                {
                    boy = trimmedLine.Split(new string[] { "|6 " }, StringSplitOptions.None)[1];

                    if (double.TryParse(en, out double sayi1)) enf3 = sayi1.ToString("F3");
                    if (double.TryParse(boy, out double sayi2)) boyf3 = sayi2.ToString("F3");
                    if (double.TryParse(th, out double sayi3)) thf3 = sayi3.ToString("F3");

                    mprLines.Add("_BSX=" + enf3);
                    mprLines.Add("_BSY=" + boyf3);
                    mprLines.Add("_BSZ=" + thf3);

                    mprLines.Add("_FNX = 0.000");
                    mprLines.Add("_FNY = 0.000");
                    mprLines.Add("_RNX = 0.000");
                    mprLines.Add("_RNY = 0.000");
                    mprLines.Add("_RNZ = 0.000");

                    mprLines.Add("_RX=" + enf3);
                    mprLines.Add("_RY=" + boyf3);

                    mprLines.Add("KM = \"X-=;X+=;Y-=;Y+=;\"");
                    mprLines.Add("KM = \"LeftWork\"");

                    mprLines.Add("");
                    mprLines.Add("[001");
                    mprLines.Add("Lo=\"" + en + "\"");
                    mprLines.Add("KM=\"\"");
                    mprLines.Add("La=\"" + boy + "\"");
                    mprLines.Add("KM=\"\"");
                    mprLines.Add("Ep=\"" + th + "\"");
                    mprLines.Add("KM=\"\"");

                    mprLines.Add("");
                    mprLines.Add("<100 \\WerkStck\\");
                    mprLines.Add("LA=\"Lo\"");
                    mprLines.Add("BR=\"La\"");
                    mprLines.Add("DI=\"Ep\"");
                    mprLines.Add("FNX=\"0\"");
                    mprLines.Add("FNY=\"0\"");
                    mprLines.Add("AX=\"0\"");
                    mprLines.Add("AY=\"0\"");

                    mprLines.Add("");

                }

                else if (trimmedLine.StartsWith("#WHILE"))  // LOOP
                {
                    if (!inWhileLoop)
                    {
                        inWhileLoop = true;
                        whileLoopIndexStart = i - 1;
                    }
                    whileLoopIndexEnd = 0;

                    for (int j = i; j < lines.Length ; j++) if (lines[j].StartsWith("#WEND")) whileLoopIndexEnd = j;

                    if (whileLoopIndexEnd == 0) // Error While Loop without WEND
                    { 
                        ShowErrorMessage("Bir WHILE döngüsü WEND ile sonlanmamış");
                        break;
                    }

                    string[] parcalar = trimmedLine.Split(' ');
                    whileCond = parcalar[1].Trim();
                    whileCond = ReplaceVariables(vars, whileCond);
                    whileCond = Calculate(whileCond);
                }

                else if (trimmedLine.StartsWith("#WEND"))  // LOOP
                {
                    whileCond = "False"; // canceling loop only one tour is enough
                    if (whileCond == "True") i = whileLoopIndexStart;
                    else inWhileLoop = false;
                }

                else if (trimmedLine.StartsWith("#") && !trimmedLine.StartsWith("#WEND"))
                {
                    string[] parcalar = trimmedLine.Split('=');
                    string varName = parcalar[0].Trim();
                    string str = parcalar[1].Trim();

                    if (str == "DX") str = en;
                    if (str == "DY") str = boy;


                    str = ReplaceVariables(vars, str);
                    if (Regex.IsMatch(str, @"[\+\-\*/]")) str = Calculate(str);



                    vars[varName] = Convert.ToDouble(str);
                }

                else if (trimmedLine.StartsWith("G101"))
                {

                    if (!inG101Block) inG101Block = true;

                    string x, y, z, xf3, yf3, zf3;
                    x = "";
                    y = "";
                    z = "";
                    xf3 = "";
                    yf3 = "";
                    zf3 = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G101") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                    }

                    // Eğer bir önceki frezeleme ile aynı ise işlem yapma
                    if (frzElementsList["x"] == x && frzElementsList["y"] == y && frzElementsList["z"] == z) continue;

                    frzElementsList["x"] = x;
                    frzElementsList["y"] = y;
                    frzElementsList["z"] = z;

                    mprLines.Add("$E" + frzOpCnt.ToString());
                    frzOpCnt++;

                    if (firstPoint)
                    {
                        mprLines.Add("KP");
                        firstPoint = false;
                    }
                    else
                    {
                        mprLines.Add("KL");
                    }

                    if (double.TryParse(x, out double sayi1)) xf3 = sayi1.ToString("F3");
                    if (double.TryParse(y, out double sayi2)) yf3 = sayi2.ToString("F3");
                    if (double.TryParse(z, out double sayi3)) zf3 = sayi3.ToString("F3");

                    mprLines.Add("X=" + xf3);
                    mprLines.Add("Y=" + yf3);
                    mprLines.Add("Z=" + thf3);
                    mprLines.Add("KO=0");

                    mprLines.Add(".X=" + xf3);
                    mprLines.Add(".Y=" + yf3);
                    mprLines.Add(".Z=" + thf3);
                    mprLines.Add(".KO=0");
                    mprLines.Add("");


                }
                else if (trimmedLine.StartsWith("G41"))
                {
                    // The milling cutter is performing an operation on the left edge.
                }
                else if (trimmedLine.StartsWith("G40"))
                {
                    // The milling cutter is performing an operation from the center.
                }
                else if (trimmedLine.StartsWith("G100"))    // Vertical Hole
                {
                    if (!inG100Block) inG101Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }


                    mprLines.Add("<102 \\BohrVert\\ \\||\\");
                    mprLines.Add("XA=\"" + x + "\"");
                    mprLines.Add("YA=\"" + y + "\"");
                    mprLines.Add("BM=\"LS\"");
                    mprLines.Add("TI=\"" + z + "\"");
                    mprLines.Add("DU=\"" + toolDia + "\"");
                    mprLines.Add("AN=\"1\"");
                    mprLines.Add("MI=\"0\"");
                    //mprLines.Add("AB=\"290.5\"");
                    mprLines.Add("XR=\"1\"");
                    mprLines.Add("YR=\"0\"");
                    mprLines.Add("S_=\"1\"");
                    mprLines.Add("KM=\" \"");


                    mprLines.Add("");
                }
                else if (trimmedLine.StartsWith("G182"))    // Left Pocket Hole
                {
                    if (!inG182Block) inG182Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }

                    string depth;
                    // The x var shows how deep it will enter on the panel. will be x tool depth

                    depth = x;
                    x = en;

                    mprLines.Add("<103 \\BohrHoriz\\ \\||\\");
                    mprLines.Add("XA=\"" + x + "\"");
                    mprLines.Add("YA=\"" + y + "\"");
                    mprLines.Add("ZA=\"" + z + "\"");
                    mprLines.Add("BM=\"XM\"");
                    mprLines.Add("TI=\"" + depth + "\"");
                    mprLines.Add("DU=\"" + toolDia + "\"");
                    mprLines.Add("AN=\"1\"");
                    mprLines.Add("MI=\"0\"");
                    //mprLines.Add("AB=\"290.5\"");
                    mprLines.Add("WI=\"0\"");
                    mprLines.Add("ANA=\"20\"");
                    mprLines.Add("KM=\" \"");


                    mprLines.Add("");
                }
                else if (trimmedLine.StartsWith("G183"))    // Right Pocket Hole
                {
                    if (!inG183Block) inG182Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }

                    string depth;
                    // The x side shows how deep it will enter on the panel. so the width will be depth of tool
                    depth = x;
                    x = "0";

                    mprLines.Add("<103 \\BohrHoriz\\ \\||\\");
                    mprLines.Add("XA=\"" + x + "\"");
                    mprLines.Add("YA=\"" + y + "\"");
                    mprLines.Add("ZA=\"" + z + "\"");
                    mprLines.Add("BM=\"XP\"");
                    mprLines.Add("TI=\"" + depth + "\"");
                    mprLines.Add("DU=\"" + toolDia + "\"");
                    mprLines.Add("AN=\"1\"");
                    mprLines.Add("MI=\"0\"");
                    //mprLines.Add("AB=\"290.5\"");
                    mprLines.Add("WI=\"0\"");
                    mprLines.Add("ANA=\"20\"");
                    mprLines.Add("KM=\" \"");


                    mprLines.Add("");
                }

            }

            mprLines.Add("<101 \\Commments:\\");
            mprLines.Add("KM=\"Original file :" + dosyaAdi + "\"");
            mprLines.Add("KM=\"Copyright VITEM\"");
            mprLines.Add("!"); // End of File

            // Combine and return the MPR content
            return string.Join("\n", mprLines);
        }


        private async Task SaveMPRFile(string dosyaAdi, string mprIcerik, StorageFolder folder)
        {
            StorageFolder altKlasor = await folder.CreateFolderAsync("MPR", CreationCollisionOption.OpenIfExists);
            StorageFile mprDosya = await altKlasor.CreateFileAsync(dosyaAdi + ".mpr", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(mprDosya, mprIcerik);
        }

        static string ReplaceVariables(Dictionary<string, double> vars, string denklem)
        {
            foreach (var kvp in vars)
            {
                string varName = kvp.Key;
                double str = kvp.Value;
                denklem = denklem.Replace(varName, str.ToString());
            }

            return denklem;
        }


        static string Calculate(string ifade)
        {
            Expression expr = new Expression(ifade);
            return Convert.ToString(expr.Evaluate());
        }

        private string ProcessCoordinatePart(string part, Dictionary<string, double> vars)
        {
            string coordinateValue = part.Substring(1);

            // Remove the "=" character if it's present at the beginning
            if (coordinateValue.Length > 0 && coordinateValue[0] == '=')
            {
                coordinateValue = coordinateValue.Substring(1);
            }

            coordinateValue = ReplaceVariables(vars, coordinateValue);

            if (Regex.IsMatch(coordinateValue, @"[\+\-\*/]") && !double.TryParse(coordinateValue, out double result))
            {
                coordinateValue = Calculate(coordinateValue);
            }

            return coordinateValue;
        }

        async void ShowErrorMessage(string errorMessage)
        {
            var dialog = new MessageDialog(errorMessage, "Hata");
            await dialog.ShowAsync();
        }

    }
}
