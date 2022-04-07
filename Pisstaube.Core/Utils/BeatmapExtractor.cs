using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

namespace Pisstaube.Utils
{
    // Used for partially extracting contents
    public class BeatmapExtractor : IDisposable
    {
        private FileStream _fileStream;
        private ZipFile _archive;
        
        public BeatmapExtractor(int setId)
        {
            _fileStream = File.OpenRead($"data/cache/{setId:x8}");
            _archive = new ZipFile(_fileStream);
        }

        private static Beatmap GrabBeatmap(Stream beatmapFile)
        {
            var beatmapDecoder = new LegacyBeatmapDecoder();
            var beatmap = beatmapDecoder.Decode(new LineBufferedReader(beatmapFile));

            beatmapFile.Close();

            return beatmap;
        }
        
        public Stream GrabThumbnail(Stream beatmapFile)
        {
            var beatmap = GrabBeatmap(beatmapFile);

            // Thumbnail code
            var thumbnailFilePath = beatmap.Metadata.BackgroundFile;
            var thumbnailEntry = _archive.GetEntry(thumbnailFilePath);
            var thumbnailFile = _archive.GetInputStream(thumbnailEntry);

            return thumbnailFile;
        }

        public Stream GrabAudio(Stream beatmapFile)
        {
            var beatmap = GrabBeatmap(beatmapFile);

            // Audio code
            var audioFilePath = beatmap.Metadata.AudioFile;
            var audioEntry = _archive.GetEntry(audioFilePath);
            var audioFile = _archive.GetInputStream(audioEntry);

            return audioFile;
        }

        public void Dispose()
        {
            _fileStream?.Dispose();
            
            GC.SuppressFinalize(this);
        }
    }
}