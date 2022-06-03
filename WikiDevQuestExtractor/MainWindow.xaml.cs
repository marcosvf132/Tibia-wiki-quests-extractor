using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Globalization.CultureInfo;

namespace WikiDevQuestExtractor
{
    class WikiQuestObject
    {
        public WikiQuestObject()
        {
        }

        [JsonProperty("name")]
        public string name { get; set; }
        [JsonProperty("aka")]
        public string aka { get; set; }

        [JsonProperty("implemented")]
        public string implemented { get; set; }

        [JsonProperty("rookgaardquest")]
        public string rookgaardquest { get; set; }

        public bool isWorldChange { get; set; }
        public double implementedVersion { get; set; }
        public string questNameTag { get; set; }

        public bool isRook()
        {
            return !string.IsNullOrEmpty(rookgaardquest) && (rookgaardquest.ToLower() == "yes" ||
                                                             rookgaardquest.ToLower() == "true" ||
                                                             rookgaardquest.ToLower() == "1");
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public string _wikiQuestsUri = String.Empty;
        public MainWindow()
        {
            InitializeComponent();
        }

        public void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (StartBox.Text.Length == 0) {
                AppendLog("[WIKI FATAL ERROR]:");
                AppendLog("No external link was found on URL bar or it's invalid.");
                return;
            }

            AppendLog("Tibia wiki quests extractor has been initialized!!!");
            AppendLog(" - External URI: " + StartBox.Text);
            AppendLog(" --> The process can take some minutes, please wait! <--");

            _wikiQuestsUri = StartBox.Text;
            StartButton.IsEnabled = false;
            StartBox.IsEnabled = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int successWorldChange = 0;
            int successMain = 0;
            int successRook = 0;
            int exceptions = 0;
            int ignored = 0;
            List<string> externalQuestsList = new List<string>();
            string outputMainText = "      MainQuest = {\n";
            string outputRookText = "      RookQuest = {\n";
            string outputWorldChangeText = "      WorldChange = {\n";
            WebClient client = new WebClient();
            try
            {
                client.Proxy = null;
                var info = GetCultureInfo("en-gb").TextInfo;
                var settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };

                try
                {
                    string content = client.DownloadString(_wikiQuestsUri);

                    // This validation ignore JSON that is irrelevant
                    if (content.Length <= 5) {
                        throw new Exception();
                    }

                    externalQuestsList = JsonConvert.DeserializeObject<List<string>>(content, settings);
                } catch (Exception ex) {
                    AppendLog("[WIKI FATAL ERROR]:");
                    AppendLog(ex.Message);
                    AppendLog("Rrror tracer:");
                    AppendLog(ex.StackTrace);
                    return;
                }

                if (externalQuestsList.Count == 0) {
                    AppendLog("[WIKI FATAL ERROR]:");
                    AppendLog("No quest was found on the external link");
                    return;
                }

                MessageBox.Show("Connection has been succesfully established!!\n" +
                    "The process can take a while!", "Connection established", MessageBoxButton.OK);
                List<WikiQuestObject> questObjects = new List<WikiQuestObject>();
                foreach (var questFullName in externalQuestsList) {

                    // Must ignore invalid quests.
                    if (questFullName.Length == 0) {

                        ignored++;
                        continue;
                    }

                    WikiQuestObject wikiQuestObject;
                    try {
                        string content = client.DownloadString(_wikiQuestsUri + questFullName.Replace(" ", "%20").Replace("'", "%27"));

                        // This validation ignore JSON that is irrelevant
                        if (content.Length <= 5) {
                            throw new Exception();
                        }

                        wikiQuestObject = JsonConvert.DeserializeObject<WikiQuestObject>(content, settings);
                    } catch {
                        // This exception can happen more than you thing. Some errors for it can be:
                        // - Item is not registered on WIKI.
                        // - Item data on wiki is irrelevant.
                        // - No internet connection.
                        // - Host refused connection.
                        // - Host could not find the item by it's name.
                        // Breaking the entire iteratior is not a good idea here because this can happen a lot.
                        exceptions++;
                        continue;
                    }

                    wikiQuestObject.isWorldChange = !questFullName.Contains("Quest");
                    if (!string.IsNullOrEmpty(wikiQuestObject.implemented)) {
                        wikiQuestObject.implemented = new string(wikiQuestObject.implemented.Where(c => (c == '.' || char.IsDigit(c))).ToArray());
                        if (wikiQuestObject.implemented.Count(f => f == '.') > 1) {
                            wikiQuestObject.implemented = wikiQuestObject.implemented.Remove(
                                wikiQuestObject.implemented.IndexOf('.', wikiQuestObject.implemented.IndexOf('.') + 1),
                                wikiQuestObject.implemented.Length - wikiQuestObject.implemented.IndexOf('.', wikiQuestObject.implemented.IndexOf('.') + 1));
                        }

                        int digitsAfterDot = (wikiQuestObject.implemented.Length - (wikiQuestObject.implemented.IndexOf('.') + 1));
                        if (digitsAfterDot > 2) {
                            wikiQuestObject.implemented = wikiQuestObject.implemented.Remove(
                                wikiQuestObject.implemented.IndexOf('.') + 3,
                                digitsAfterDot - 2);
                        } else if (digitsAfterDot < 2) {
                            for (int i = 1; i <= digitsAfterDot; i++) {
                                wikiQuestObject.implemented = wikiQuestObject.implemented + "0";
                            }
                        }
                        wikiQuestObject.implementedVersion = double.Parse(wikiQuestObject.implemented);
                    } else {
                        wikiQuestObject.implementedVersion = 0;
                    }

                    wikiQuestObject.questNameTag = questFullName.Replace(' ', '_').Replace("'", "").Replace("...", "").Replace("-","");
                    if (char.IsNumber(wikiQuestObject.questNameTag.First())) {
                        wikiQuestObject.questNameTag = "_" + wikiQuestObject.questNameTag;
                    }

                    if (wikiQuestObject.isRook())
                        successRook++;
                    else if (wikiQuestObject.isWorldChange)
                        successWorldChange++;
                    else
                        successMain++;

                    questObjects.Add(wikiQuestObject);
                }

                questObjects.Sort((x, y) => x.implementedVersion.CompareTo(y.implementedVersion));
                foreach (var questObj in questObjects) {

                    if (questObj.implementedVersion != 0) {
                        if (questObj.isRook())
                            outputRookText = outputRookText + "         -- Implemented on " + questObj.implemented + "\n";
                        else if (questObj.isWorldChange)
                            outputWorldChangeText = outputWorldChangeText + "         -- Implemented on " + questObj.implemented + "\n";
                        else
                            outputMainText = outputMainText + "         -- Implemented on " + questObj.implemented + "\n";
                    }

                    if (questObj.isRook())
                        outputRookText = outputRookText + "         -- '" + questObj.name;
                    else if (questObj.isWorldChange)
                        outputWorldChangeText = outputWorldChangeText + "         -- '" + questObj.name;
                    else
                        outputMainText = outputMainText + "         -- '" + questObj.name;
                    if (!string.IsNullOrEmpty(questObj.aka) && questObj.aka.Length > 1) {
                        if (questObj.isRook())
                            outputRookText = outputRookText + "' also known as '" + questObj.aka.Replace("[[", "").Replace("]]", "") + "'\n";
                        else if (questObj.isWorldChange)
                            outputWorldChangeText = outputWorldChangeText + "' also known as '" + questObj.aka.Replace("[[", "").Replace("]]", "") + "'\n";
                        else
                            outputMainText = outputMainText + "' also known as '" + questObj.aka.Replace("[[", "").Replace("]]", "") + "'\n";
                    } else {
                        if (questObj.isRook())
                            outputRookText = outputRookText + "'\n";
                        else if (questObj.isWorldChange)
                            outputWorldChangeText = outputWorldChangeText + "'\n";
                        else
                            outputMainText = outputMainText + "'\n";
                    }

                    if (questObj.isRook()) {
                        outputRookText = outputRookText + "         " + questObj.questNameTag + " = {\n";
                        outputRookText = outputRookText + "            -- To be implemented\n";
                        outputRookText = outputRookText + "         },\n";
                        outputRookText = outputRookText + "\n";
                    } else if (questObj.isWorldChange) {
                        outputWorldChangeText = outputWorldChangeText + "         " + questObj.questNameTag + " = {\n";
                        outputWorldChangeText = outputWorldChangeText + "            -- To be implemented\n";
                        outputWorldChangeText = outputWorldChangeText + "         },\n";
                        outputWorldChangeText = outputWorldChangeText + "\n";
                    } else {
                        outputMainText = outputMainText + "         " + questObj.questNameTag + " = {\n";
                        outputMainText = outputMainText + "            -- To be implemented\n";
                        outputMainText = outputMainText + "         },\n";
                        outputMainText = outputMainText + "\n";
                    }
                }
            } catch (Exception ex) {
                AppendLog("[WIKI ERROR]:");
                AppendLog(ex.Message);
                AppendLog("Rrror tracer:");
                AppendLog(ex.StackTrace);
            }
            AppendLog("Wiki API: (" + externalQuestsList.Count + " quests)");
            AppendLog("- " + (successMain + successRook + successWorldChange).ToString() + " quests pushed.");
            AppendLog("- " + exceptions + " exceptions.");
            AppendLog("- " + ignored + " ignored.");
            AppendLog("");
            AppendLog("Time elapsed: " + TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds).ToString(@"hh\:mm\:ss"));
            AppendLog("");
            AppendLog("* Exceptions can happen due to:");
            AppendLog("- Internet connection has failed.");
            AppendLog("- API refused connection.");
            AppendLog("- API could not find the quest by it's name.");
            AppendLog("- Quest not registered on wiki.");
            AppendLog("- Quests with custom names.");
            client.Dispose();
            stopwatch.Stop();
            outputWorldChangeText = outputWorldChangeText + "      },\n";
            outputRookText = outputRookText + "      },\n";
            outputMainText = outputMainText + "      }\n";
            outputMainText = outputMainText + "   }\n";
            outputMainText = outputRookText + outputWorldChangeText + outputMainText;
            outputMainText = "   Quest = {\n" + outputMainText;
            outputMainText = "   -- Worldchange Quests registered: " + successWorldChange + "\n" + outputMainText;
            outputMainText = "   -- Rookguard Quests registered: " + successRook + "\n" + outputMainText;
            outputMainText = "   -- Mainland Quests registered: " + successMain + "\n" + outputMainText;
            outputMainText = "   -- List generated by Marcosvf132\n" + outputMainText;

            if ((successMain + successRook + successWorldChange) > 0) {
                var externalFile = File.CreateText(Environment.CurrentDirectory + "\\output.lua");
                externalFile.Write(outputMainText);
                externalFile.Close();
                externalFile.Dispose();
            }
        }
        public void AppendLog(string text)
        {
            TextBlock block = new TextBlock();
            block.Text = text;
            LogPanel.Children.Add(block);
        }
    }
}
