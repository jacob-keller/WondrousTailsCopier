using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Configuration;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using Lumina.Excel.Sheets;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLookingForGroup;

namespace WondrousTailsCopier.Windows;

public class ComparisonWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;
    private List<UInt32> playerColors;
    private List<string> trials;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ComparisonWindow(Plugin plugin) : base("Wondrous Tails Book Club###With a constant ID", 
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
        Configuration = plugin.Configuration;

        // UInt32 colors are reversed: 0xAABBGGRR
        playerColors = new List<UInt32>() {
            0xFFFCAF64,
            0xFF91FEFF,
            0xFF7C7CFC,
            0xFFF52B73,
            0xFF8EFBA1,
            0xFFF88AEE,
            0xFF8036EA,
            0xFFF52300,
        };

        trials = GetTrials();
    }

    public void Dispose() { }

    private void Redraw()
    {
        Toggle();
        Toggle();
    }
    private List<string> GetTrials()
    {
        List<string> trials = new List<string>();
        var territories = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()!
            .Where(r => r.ContentType.Value.RowId == 4)
            .Select(r => r.TerritoryType.Value)
            .ToHashSet();

        foreach (var territory in territories)
        {
            trials.Add(territory.ContentFinderCondition.Value.Name.ToString());
        }

        return trials;
    }
    //private void DisplayObjectives(Dictionary<string, string> dungeonObjectives, Dictionary<string, string> allianceRaidObjectives, 
    //    Dictionary<string, string> normalRaidObjectives, Dictionary<string, string> trialObjectives, Dictionary<string, string> otherObjectives)
    private void DisplayObjectives(List<Dictionary<string, string>> categorizedObjectives)
    {
        var allBooks = Configuration.AllBooks;

        for (var i = 0; i < allBooks.Count; i++)
        {
            foreach (var book in allBooks[i])
            {
                var playerName = book.Key;
                ImGui.Button(playerName);
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                ImGui.GetWindowDrawList().AddLine(min, max, playerColors[i], 8.0f);

                ImGui.SameLine();
            }
        }

        ImGui.Text(" ");
        ImGui.Text(" ");

        foreach (var objectiveDict in categorizedObjectives)
        {
            var sortedObjectiveDict = new SortedDictionary<string, string>(objectiveDict);

            foreach (var objective in sortedObjectiveDict)
            {
                var ids = objective.Value.Split(',');
                ImGui.Button(objective.Key);
                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                min.Y = max.Y;

                for (var i = 0; i < ids.Length; i++)
                {
                    ImGui.GetWindowDrawList().AddLine(min, max, playerColors[int.Parse(ids[i])], 8.0f);
                    //min.X += 6.0f;
                    //Plugin.Chat.Print($"{min.X.ToString()}, {min.Y.ToString()} // {max.X.ToString()}, {max.Y.ToString()}");
                    min.X += ((max.X - min.X) / ids.Length);
                }
                ImGui.SameLine();
            }
            ImGui.Text(" ");
            ImGui.Text(" ");
        }
    }
    private void OrganizeObjectives()
    {
        var allObjectives = Configuration.AllObjectives;
        var dungeonObjectives = new Dictionary<string, string>() { };
        var allianceRaidObjectives = new Dictionary<string, string>() { };
        var normalRaidObjectives = new Dictionary<string, string>() { };
        var trialObjectives = new Dictionary<string, string>() { };
        var otherObjectives = new Dictionary<string, string>() { };

        foreach (var objective in allObjectives)
        {
            var pattern = @"^need \d";
            var r = new Regex(pattern);
            var m = r.Match(objective.Key);
            if (m.Success)
            {
                continue;
            }
            
            pattern = @"^\d+";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                dungeonObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            pattern = @" AR$";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                allianceRaidObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            pattern = @"^FL$|^CC$|RW|^Deep Dungeons$|^Treasure";
            r = new Regex(pattern);
            m = r.Match(objective.Key);
            if (m.Success)
            {
                otherObjectives.Add(objective.Key, objective.Value);
                continue;
            }

            var foundTrial = false;
            foreach (var trial in trials)
            {                
                if (trial.Contains(objective.Key))
                {
                    trialObjectives.Add(objective.Key, objective.Value);
                    foundTrial = true;
                }
                if (foundTrial)
                {
                    break;
                }
            }
            if (foundTrial)
            {
                continue;
            }

            normalRaidObjectives.Add(objective.Key, objective.Value);
        }
        DisplayObjectives(new List<Dictionary<string, string>>() { dungeonObjectives, allianceRaidObjectives, normalRaidObjectives, trialObjectives, otherObjectives } );
    }
    private void CompileBooks()
    {
        var allBooks = Configuration.AllBooks;
        var allObjectives = new Dictionary<string, string>() { };

        for (var i = 0; i < allBooks.Count; i++)
        {
            foreach (var book in allBooks[i])
            {
                var playerName = book.Key;
                var playerObjectives = book.Value;
                var splitObjs = playerObjectives.Split(", ");

                foreach (var obj in splitObjs)
                {
                    if (allObjectives.ContainsKey(obj))
                    {
                        if (!allObjectives[obj].Contains(i.ToString()))
                        {
                            if (allObjectives[obj].Length > 0)
                            {
                                allObjectives[obj] += $",{i.ToString()}";
                            }
                            else
                            {
                                allObjectives[obj] = i.ToString();
                            }
                        }
                    }
                    else
                    {
                        allObjectives.Add(obj, i.ToString());
                    }
                }
            }
        }
        Configuration.AllObjectives = allObjectives;
        Configuration.Save();
    }
    private void ParseClipboard()
    {
        var allBooks = Configuration.AllBooks;

        var pattern = @"(\[\d+:\d+\]\[\w+\d\]|\[\d+:\d+\]|\[\w+\d\])(\(\W?(\w+ \w+)\) |<\W?(\w+ \w+)> )(.*)";
        var clipboardContents = ImGui.GetClipboardText().Trim();

        var r = new Regex(pattern);
        var m = r.Match(clipboardContents);

        var playerName = "";
        var playerIndex = -1;

        var playerObjectives = m.Groups[5].Value;
        if (m.Success)
        {
            if (m.Groups[3].Value.Length == 0)
            {
                playerName = m.Groups[4].Value;
            }
            else
            {
                playerName = m.Groups[3].Value;
            }

            for (var i = 0; i < allBooks.Count; i++)
            {
                if (allBooks[i].ContainsKey(playerName))
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex > -1)
            {
                allBooks[playerIndex][playerName] = playerObjectives;
            }
            else
            {
                allBooks.Add(new Dictionary<string, string> {{ playerName, playerObjectives }});
            }

            Configuration.AllBooks = allBooks;
            Configuration.Save();
        }
    }

    public override void Draw()
    {
        var allBooks = Configuration.AllBooks;
        var testerValue = Configuration.Tester;

        if (ImGui.Button("Import from Clipboard"))
        {
            Plugin.Chat.Print("Import from Clipboard!");

            ParseClipboard();
            CompileBooks();
            OrganizeObjectives();
            Redraw();
        }
        ImGui.SameLine();
        if (ImGui.Button("Remove all"))
        {
            Configuration.AllBooks = [];
            Configuration.AllObjectives = [];
            Configuration.Save();
            Redraw();
        }

        if (allBooks != null)
        {
            OrganizeObjectives();
        }
        else
        {
            Configuration.AllBooks = [];
            Configuration.AllObjectives = [];
            Configuration.Save();
            Redraw();
        }


        /*
        ImGui.Text("Import Wondrous Tails Objectives from Clipboard");
        ImGui.Text(allBooks.Count.ToString());
        ImGui.SameLine();

        //if (ImGuiComponents.IconButton(FontAwesomeIcon.Clipboard))
        if (ImGui.Button("Import from Clipboard"))
        {
            Plugin.Chat.Print("Import from Clipboard!");

            ParseClipboard();
            CompileBooks();
            OrganizeObjectives();
            Redraw();
        }

        if (ImGui.Button("Remove all"))
        {
            Configuration.AllBooks = [];
            Configuration.AllObjectives = [];
            Configuration.Save();
            Redraw();
        }
        */
        /*
        ImGui.Text("Julianna Arashi");
        //ImGui.SameLine();
        ImGui.Button("70-90");
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        min.Y = max.Y;

        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[0], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[1], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[2], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[3], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[4], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[5], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[6], 8.0f);
        min.X += 6.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, playerColors[7], 8.0f);
        min.X -= 42.0f;
        ImGui.GetWindowDrawList().AddLine(min, max, 0xFFFFFFFF, 8.0f);

        ImGui.SameLine();
        ImGui.Text("70-90");
        ImGui.SameLine();
        ImGui.Text("70-90");
        ImGui.SameLine();
        ImGui.Text("70-90");

        ImGui.Text("ARR AR");
        ImGui.SameLine();
        ImGui.Text("EW AR");
        
        ImGui.Text("Binding Coil");
        ImGui.SameLine();
        ImGui.Text("Eden's Promise");
        ImGui.SameLine();
        ImGui.Text("AAC Light-heavyweight M1 or M2");

        ImGui.Text("Crown of the Immaculate");
        ImGui.SameLine();
        ImGui.Text("Zodiark's Fall");
        ImGui.SameLine();
        ImGui.Text("Mount Ordeals");
        ImGui.SameLine();
        ImGui.Text("Hells Kier");

        ImGui.Text("Deep Dungeons");
        ImGui.SameLine();
        ImGui.Text("CC");
        */
    }
}
