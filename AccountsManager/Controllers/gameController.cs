using AccountsManager.Data;
using AccountsManager.Models;
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;


namespace AccountsManager.Controllers
{

    [ApiController]
    [Route("api/games")]

    public class GameController : ControllerBase
    {
        private readonly Sql _sql;
        private readonly IWebHostEnvironment _env;
        private readonly string _gameBuildsFolder;

        public GameController(Sql sql, IWebHostEnvironment env)
        {
            _sql = sql;
            _env = env;

            // Folder for uploaded games
            _gameBuildsFolder = Path.Combine(Directory.GetCurrentDirectory(), "GameBuilds");
            if (!Directory.Exists(_gameBuildsFolder))
            {
                Directory.CreateDirectory(_gameBuildsFolder);
            }
        }





        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                using SqlConnection conn = await _sql.GetOpenConnectionAsync();
                using var cmd = new SqlCommand("SELECT GameID, GameCreationDate, GameName, PlayersViews FROM games;", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                var list = new List<games>();
                while (await reader.ReadAsync())
                {
                    list.Add(new games
                    {
                        GameID = reader.GetInt32(0),
                        GameCreationDate = reader.IsDBNull(1) ? DateTime.UtcNow : reader.GetDateTime(1),
                        GameName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        PlayersViews = reader.IsDBNull(3) ? 0 : reader.GetInt32(3)
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching games: " + ex.Message);
                return StatusCode(500, "Internal server error while fetching games.");
            }
        }
        [HttpDelete("{gameName}")]
        public async Task<IActionResult> Delete([FromRoute] string gameName)
        {
            gameName = Uri.UnescapeDataString(gameName).Trim();

            if (string.IsNullOrWhiteSpace(gameName))
                return BadRequest(new { message = "Game name is required." });

            using var conn = await _sql.GetOpenConnectionAsync();

            string folderName;
            using (var cmd = new SqlCommand(
                "SELECT uploads FROM games WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS", conn))
            {
                cmd.Parameters.AddWithValue("@name", gameName);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return NotFound(new { message = "Game not found." });

                folderName = (string)result;
            }

            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "GameBuilds", folderName);
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);

            using var deleteCmd = new SqlCommand(
                "DELETE FROM games WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS", conn);
            deleteCmd.Parameters.AddWithValue("@name", gameName);
            await deleteCmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Game deleted successfully." });
        }


        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] IFormFile gamefile, [FromForm] string gamename)
        {
            if (string.IsNullOrEmpty(gamename))
                return BadRequest(new { error = "Game name is required" });

            if (gamefile == null || gamefile.Length == 0)
                return BadRequest(new { error = "No game file uploaded" });

            if (!gamefile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .zip files are supported" });

            var gameId = Guid.NewGuid().ToString("N");
            var gameFolder = Path.Combine(_env.WebRootPath, "GameBuilds", gameId);
            Directory.CreateDirectory(gameFolder);

            var tempZipPath = Path.Combine(gameFolder, gamefile.FileName);

            // ✅ Save ZIP safely and ensure it’s fully closed
            await using (var stream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await gamefile.CopyToAsync(stream);
            }

            // Small safeguard delay to ensure handle release (optional but safe)
            await Task.Delay(100);

            try
            {
                // ✅ Open ZIP in Read mode to avoid file lock issues
                using (var zipStream = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(gameFolder);
                }

                System.IO.File.Delete(tempZipPath);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to unzip game build: " + ex.Message });
            }

            // Locate index.html
            var indexPath = Directory.GetFiles(gameFolder, "index.html", SearchOption.AllDirectories).FirstOrDefault();
            if (indexPath == null)
                return BadRequest(new { error = "index.html not found" });

            // ✅ Flatten nested Build folders
            var buildDirs = Directory.GetDirectories(gameFolder, "Build", SearchOption.AllDirectories);
            foreach (var dir in buildDirs)
            {
                var subBuild = Path.Combine(dir, "Build");
                if (Directory.Exists(subBuild))
                {
                    foreach (var file in Directory.GetFiles(subBuild))
                    {
                        var dest = Path.Combine(dir, Path.GetFileName(file));
                        if (System.IO.File.Exists(dest))
                            System.IO.File.Delete(dest);
                        System.IO.File.Move(file, dest);
                    }
                    Directory.Delete(subBuild, true);
                }
            }

            // ✅ Fix Unity HTML paths
            try
            {
                string html = await System.IO.File.ReadAllTextAsync(indexPath);
                html = html.Replace("Build/", "./Build/")
                           .Replace("TemplateData/", "./TemplateData/")
                           .Replace("StreamingAssets/", "./StreamingAssets/");
                await System.IO.File.WriteAllTextAsync(indexPath, html);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to fix index.html paths: " + ex.Message });
            }

            // Return final URL
            var relativePath = indexPath.Substring(_env.WebRootPath.Length).Replace("\\", "/");
            var gameUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";

            return Ok(new
            {
                gameId = gameId,
                gameUrl = gameUrl
            });
        }




    }
}


