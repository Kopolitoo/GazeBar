using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GazeBar
{
    /// <summary>
    /// MODÈLE : enregistre les positions du curseur pendant quelques secondes et les
    /// écrit dans un fichier CSV (tableau lisible avec Excel ou un script).
    ///
    /// À quoi ça sert : mesurer objectivement la qualité du « regard » obtenu par le
    /// curseur : cadence réelle, régularité, précision, zone couverte. Chaque ligne du
    /// fichier est un changement de position (donc les écarts de temps entre lignes
    /// donnent la cadence réelle).
    /// </summary>
    internal sealed class CursorRecorder
    {
        private readonly object _lock = new object();
        private readonly List<CursorSample> _samples = new List<CursorSample>();
        private bool _recording;

        /// <summary>Commence l'enregistrement (la position actuelle est notée comme première ligne).</summary>
        public void Start(CursorSampler sampler)
        {
            int x;
            int y;
            sampler.GetLatest(out x, out y);

            CursorSample first = new CursorSample();
            first.Ms = sampler.NowMs;
            first.X = x;
            first.Y = y;

            lock (_lock)
            {
                _samples.Clear();
                _samples.Add(first);
                _recording = true;
            }

            // « += » : à chaque changement de position, la méthode OnPositionChanged sera appelée.
            sampler.PositionChanged += OnPositionChanged;
        }

        /// <summary>Appelée pour chaque changement de position (depuis le thread d'échantillonnage).</summary>
        private void OnPositionChanged(CursorSample sample)
        {
            lock (_lock)
            {
                if (_recording)
                {
                    _samples.Add(sample);
                }
            }
        }

        /// <summary>Interrompt l'enregistrement sans rien sauvegarder.</summary>
        public void Cancel(CursorSampler sampler)
        {
            sampler.PositionChanged -= OnPositionChanged;
            lock (_lock)
            {
                _recording = false;
            }
        }

        /// <summary>Termine l'enregistrement et écrit le fichier CSV. Renvoie le chemin du fichier.</summary>
        public string StopAndSave(CursorSampler sampler, double screenWidth, double screenHeight)
        {
            Cancel(sampler);

            List<CursorSample> copy;
            lock (_lock)
            {
                copy = new List<CursorSample>(_samples);
            }

            // Le dossier « Documents\GazeBar ».
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GazeBar");
            Directory.CreateDirectory(folder);

            string file = Path.Combine(
                folder, "positions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");

            // InvariantCulture : le séparateur décimal est toujours le point, quelle que soit
            // la langue de Windows (sinon la virgule française casserait le format CSV).
            CultureInfo inv = CultureInfo.InvariantCulture;

            StringBuilder text = new StringBuilder();
            text.AppendLine("# ecran=" + screenWidth.ToString("F0", inv) + "x" + screenHeight.ToString("F0", inv));
            text.AppendLine("t_ms,x,y");

            double t0 = copy.Count > 0 ? copy[0].Ms : 0;
            foreach (CursorSample s in copy)
            {
                text.AppendLine(
                    (s.Ms - t0).ToString("F2", inv) + "," +
                    s.X.ToString(inv) + "," +
                    s.Y.ToString(inv));
            }

            File.WriteAllText(file, text.ToString(), Encoding.UTF8);
            App.Log("Enregistrement : " + copy.Count + " positions dans " + file);
            return file;
        }
    }
}
