using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace RelicLoadoutJournal
{
    internal sealed class CompletedRun
    {
        public Guid RunId { get; set; }
        public DateTime CompletedUtc { get; set; }
        public int Kingdom { get; set; }
        public string Hero { get; set; }
        public int FinalDay { get; set; }
        public string[] Relics { get; set; }

        internal bool IsValid => RunId != Guid.Empty && FinalDay >= 1 &&
            CompletedUtc != default && !string.IsNullOrWhiteSpace(Hero) && Relics != null &&
            Array.TrueForAll(Relics, id => !string.IsNullOrWhiteSpace(id));
    }

    internal sealed class Journal
    {
        private readonly string directory;

        internal Journal(string root, int profile)
        {
            directory = Path.Combine(root, "profile-" + profile.ToString(CultureInfo.InvariantCulture));
        }

        // A single committed filename is the deduplication key, even across game restarts.
        internal bool Append(CompletedRun run)
        {
            if (!run.IsValid) throw new InvalidDataException("Incomplete run summary.");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, run.RunId.ToString("D") + ".json");
            if (File.Exists(target)) return false;
            string temp = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(run, Formatting.Indented));
                try { File.Move(temp, target); }
                catch (IOException) when (File.Exists(target)) { return false; }
                return true;
            }
            finally { File.Delete(temp); }
        }

        internal List<CompletedRun> Read(Action<string> warn)
        {
            var runs = new List<CompletedRun>();
            if (!Directory.Exists(directory)) return runs;
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    var run = JsonConvert.DeserializeObject<CompletedRun>(File.ReadAllText(path));
                    if (run == null || !run.IsValid ||
                        !Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var key) || key != run.RunId)
                        throw new InvalidDataException("Invalid run summary.");
                    runs.Add(run);
                }
                catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException || e is InvalidDataException)
                {
                    warn(Path.GetFileName(path) + ": " + e.Message);
                }
            }
            runs.Sort((a, b) => {
                int order = b.CompletedUtc.CompareTo(a.CompletedUtc);
                return order != 0 ? order : a.RunId.CompareTo(b.RunId);
            });
            return runs;
        }
    }
}
