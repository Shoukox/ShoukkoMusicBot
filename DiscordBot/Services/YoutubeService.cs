﻿using Discord.Audio;
using DiscordBot.Services.Types;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode;
using System.Diagnostics;
using System.IO;
using static System.Net.WebRequestMethods;
using FFmpeg.AutoGen;
using AngleSharp.Dom;
using CliWrap;

namespace DiscordBot.Services
{
    public class YoutubeService(FfmpegService ffmpegService, ILogger<YoutubeService> logger)
    {
        public static bool CookiesExists = System.IO.File.Exists("cookies.txt");
        public const string YtDlpFileName = "yt-dlp";
        public const string FfmpegFileName = "ffmpeg";

        private string? _searchQuery { get; set; }
        private bool _isUri { get; set; }

        public string GetCommandArgumentsForYTDLP(string query, bool isUri)
        {
            if (!isUri) query = $"ytsearch:\"{query}\"";
            if (CookiesExists) query = $"--cookies cookies.txt {query}";
            return $"--quiet -o - {query}";
        }
        public string GetCommandArgumentsForYTDLP_SongTitle(string query, bool isUri)
        {
            if (!isUri) query = $"ytsearch:\"{query}\"";
            if (CookiesExists) query = $"--cookies cookies.txt {query}";
            return $"--print title {query}";
        }
        public string GetCommandArgumentsForFFMPEG = $"-hide_banner -loglevel panic -i pipe:0 -ac 2 -y -f s16le -ar 48000 pipe:1";

        public PlayableSong? GetSongFromYoutube(string searchQuery, bool isUri)
        {
            _isUri = isUri;
            _searchQuery = searchQuery;

            PlayableSong? playableSong = new PlayableSong()
            {
                AudioStream = GetStandardOutputForFfmpegConvertation(),
                SongTitle = GetSongTitleFromYoutube(),
            };
            return playableSong;
        }

        private Stream GetStandardOutputForFfmpegConvertation()
        {
            Process ytDlp = GetCommandProcess(GetCommandArgumentsForYTDLP(_searchQuery!, _isUri));
            ytDlp.StartInfo.RedirectStandardOutput = true;
            ytDlp.Start();
            StreamReader ytDlpStandardOutput = ytDlp.StandardOutput;

            Process ffmpeg = ffmpegService.GetCommandProcess(GetCommandArgumentsForFFMPEG);
            ffmpeg.StartInfo.RedirectStandardInput = true;
            ffmpeg.StartInfo.RedirectStandardOutput = true;
            ffmpeg.Start();

            _ = Task.Run(async() =>
            {
                try
                {
                    byte[] buffer = new byte[3840];
                    int bytesRead;
                    while ((bytesRead = await ytDlpStandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await ffmpeg.StandardInput.BaseStream.WriteAsync(buffer, 0, bytesRead);
                    }
                }
                catch(Exception e)
                {
                    logger.LogError(e, e.Message);
                }
                //ffmpeg.StandardInput.Close();

                ytDlp.Dispose();
                ffmpeg.Dispose();
            });
            return ffmpeg.StandardOutput.BaseStream;
        }

        public Process GetCommandProcess(string args)
        {
            Process ytDlp = new Process();
            ytDlp.StartInfo.FileName = YtDlpFileName;
            ytDlp.StartInfo.Arguments = args;
            ytDlp.StartInfo.UseShellExecute = false;
            return ytDlp;
        }

        private string GetSongTitleFromYoutube()
        {
            Process ytDlp = new Process();
            ytDlp.StartInfo.FileName = YtDlpFileName;
            ytDlp.StartInfo.Arguments = GetCommandArgumentsForYTDLP_SongTitle(_searchQuery!, _isUri);
            ytDlp.StartInfo.UseShellExecute = false;
            ytDlp.StartInfo.RedirectStandardOutput = true;
            ytDlp.Start();
            return ytDlp.StandardOutput.ReadToEnd();
        }
    }
}
