using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

var src = args[0]; var dst = args[1];
var root = JsonNode.Parse(File.ReadAllText(src))!.AsObject();
root["missionId"] = "bot_mission_clipview";
root["missionName"] = "Clip Viewer Map";
root["generatedStepGraphHash"] = "clipview-1";
root["steps"] = new JsonArray();

var keep = new[] { "start", "set_team_red", "spawn_bots", "defense_phase" };
var nodes = root["graph"]!["nodes"]!.AsArray();
for (int i = nodes.Count - 1; i >= 0; i--)
    if (!keep.Contains(nodes[i]!["nodeId"]!.GetValue<string>())) nodes.RemoveAt(i);

foreach (var n in nodes)
{
    var id = n!["nodeId"]!.GetValue<string>();
    if (id == "spawn_bots")
    {
        var spawns = n["botSpawns"]!.AsArray();
        for (int i = spawns.Count - 1; i >= 0; i--)
            if (spawns[i]!["botInstanceId"]!.GetValue<string>() != "blue_enemy_1") spawns.RemoveAt(i);
        spawns[0]!["displayName"] = "Viewer";
        n["botInvincible"] = true;
    }
    if (id == "build_base")
    {
        n["buildPhaseDuration"] = 4.0;
        n["duration"] = 4.0;
        n["skipPregameCutscene"] = true;
    }
    if (id == "defense_phase")
    {
        n["attackWaveDuration"] = 3600.0;
        n["skipPregameCutscene"] = true;
        n["infiniteWaves"] = true;
    }
}

var bots = root["bots"]!.AsArray();
for (int i = bots.Count - 1; i >= 0; i--)
    if (bots[i]!["botId"]!.GetValue<string>() != "blue_enemy_1") bots.RemoveAt(i);
bots[0]!["displayName"] = "Viewer";

var links = root["graph"]!["links"]!.AsArray();
JsonNode Link(string id, string from, string to) => new JsonObject { ["linkId"] = id, ["fromNodeId"] = from, ["fromPort"] = "exec", ["toNodeId"] = to, ["toPort"] = "exec" };
links.Clear();
links.Add(Link("link_start_team", "start", "set_team_red"));
links.Add(Link("link_team_spawn", "set_team_red", "spawn_bots"));
links.Add(Link("link_spawn_defense", "spawn_bots", "defense_phase"));

File.WriteAllText(dst, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("nodes=" + nodes.Count + " links=" + links.Count + " bots=" + bots.Count);
