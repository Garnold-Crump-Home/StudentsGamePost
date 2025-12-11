using AccountsManager.Data;
using AccountsManager.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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

         
            _gameBuildsFolder = Path.Combine(_env.WebRootPath ?? Directory.GetCurrentDirectory(), "GameBuilds");
            if (!Directory.Exists(_gameBuildsFolder))
            {
                Directory.CreateDirectory(_gameBuildsFolder);
            }
        }

     
        [HttpGet("all")]
        public async Task<IActionResult> GetAllGames()
        {
            try
            {
                using var conn = await _sql.GetOpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    SELECT GameName, PlayersViews, uploads
                    FROM games
                    ORDER BY ISNULL(PlayersViews, 0) DESC;
                ", conn);

                var reader = await cmd.ExecuteReaderAsync();
                var games = new List<object>();

                while (await reader.ReadAsync())
                {
                    games.Add(new
                    {
                        name = reader["GameName"].ToString(),
                        playersViews = Convert.ToInt32(reader["PlayersViews"]),
                        uploads = reader["uploads"].ToString()
                    });
                }

                return Ok(games);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching games: " + ex.Message);
                return StatusCode(500, new { message = "Internal server error." });
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

            var folderPath = Path.Combine(_gameBuildsFolder, folderName);
            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);

            using var deleteCmd = new SqlCommand(
                "DELETE FROM games WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS", conn);
            deleteCmd.Parameters.AddWithValue("@name", gameName);
            await deleteCmd.ExecuteNonQueryAsync();

            return Ok(new { message = "Game deleted successfully." });
        }

      
        [HttpPatch("{gameName}/incrementViews")]
        public async Task<IActionResult> IncrementViews([FromRoute] string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return BadRequest(new { message = "Game name is required." });

            gameName = Uri.UnescapeDataString(gameName).Trim();

            try
            {
                using var conn = await _sql.GetOpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE games
                    SET PlayersViews = ISNULL(PlayersViews, 0) + 1
                    WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS;
                    SELECT PlayersViews FROM games WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS;
                ", conn);

                cmd.Parameters.AddWithValue("@name", gameName);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return NotFound(new { message = "Game not found." });

                int updatedViews = Convert.ToInt32(result);

                return Ok(new { playersViews = updatedViews });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error incrementing views: " + ex.Message);
                return StatusCode(500, new { message = "Internal server error while incrementing views." });
            }
        }

    
        [HttpGet("{gameName}/getViews")]
        public async Task<IActionResult> GetViews([FromRoute] string gameName)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return BadRequest(new { message = "Game name is required." });

            gameName = Uri.UnescapeDataString(gameName).Trim();

            try
            {
                using var conn = await _sql.GetOpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    SELECT PlayersViews FROM games 
                    WHERE LTRIM(RTRIM(GameName))=@name COLLATE SQL_Latin1_General_CP1_CI_AS;
                ", conn);

                cmd.Parameters.AddWithValue("@name", gameName);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null || result == DBNull.Value)
                    return NotFound(new { message = "Game not found." });

                int views = Convert.ToInt32(result);
                return Ok(new { playersViews = views });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error fetching views: " + ex.Message);
                return StatusCode(500, new { message = "Internal server error while fetching views." });
            }
        }

       
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
     [FromForm] IFormFile gamefile,
     [FromForm] IFormFile? gameimage,
     [FromForm] string gamename)
        {
            if (string.IsNullOrWhiteSpace(gamename))
                return BadRequest(new { error = "Game name is required." });

            if (gamefile == null || gamefile.Length == 0)
                return BadRequest(new { error = "No game file uploaded." });

            if (!gamefile.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Only .zip files are supported." });

          
            var gameId = Guid.NewGuid().ToString("N");
            var gameFolder = Path.Combine(_gameBuildsFolder, gameId);
            Directory.CreateDirectory(gameFolder);

          
            var tempZipPath = Path.Combine(gameFolder, gamefile.FileName);

            try
            {
                await using (var fileStream = new FileStream(
                    tempZipPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await gamefile.CopyToAsync(fileStream);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to save zip file: " + ex.Message });
            }

          
            try
            {
                using var conn = await _sql.GetOpenConnectionAsync();
                using var cmd = new SqlCommand(@"
            INSERT INTO games (GameName, GameCreationDate, PlayersViews, uploads)
            VALUES (@name, GETUTCDATE(), 0, @folder);
        ", conn);

                cmd.Parameters.AddWithValue("@name", gamename);
                cmd.Parameters.AddWithValue("@folder", gameId);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Database insert failed: " + ex.Message });
            }

        
            try
            {
                
                ZipFile.ExtractToDirectory(tempZipPath, gameFolder, true);

           
                GC.Collect();
                GC.WaitForPendingFinalizers();

                System.IO.File.Delete(tempZipPath);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to unzip game build: " + ex.Message });
            }

            
            string? gameImageUrl = null;

            if (gameimage != null && gameimage.Length > 0)
            {
                var ext = Path.GetExtension(gameimage.FileName).ToLower();
                var validTypes = new[] { ".png", ".jpg", ".jpeg", ".webp" };

                if (!validTypes.Contains(ext))
                    return BadRequest(new { error = "Game image must be PNG, JPG, JPEG, or WEBP" });

                var imagePath = Path.Combine(gameFolder, "gameimage" + ext);

                try
                {
                    await using var imgStream = new FileStream(
                        imagePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);

                    await gameimage.CopyToAsync(imgStream);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = "Failed to save game image: " + ex.Message });
                }

                gameImageUrl = $"{Request.Scheme}://{Request.Host}/GameBuilds/{gameId}/gameimage{ext}";
            }

            
            var indexPath = Directory.GetFiles(gameFolder, "index.html", SearchOption.AllDirectories)
                                     .FirstOrDefault();

            if (indexPath == null)
                return BadRequest(new { error = "index.html not found in unpacked build." });

            try
            {
                var html = await System.IO.File.ReadAllTextAsync(indexPath);

                html = html.Replace("Build/", "./Build/")
                           .Replace("TemplateData/", "./TemplateData/")
                           .Replace("StreamingAssets/", "./StreamingAssets/");

                await System.IO.File.WriteAllTextAsync(indexPath, html);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Failed to rewrite index.html: " + ex.Message });
            }

        
            var rootPath = _env.WebRootPath ?? Directory.GetCurrentDirectory();
            var relativePath = indexPath.Substring(rootPath.Length).Replace("\\", "/");
            var gameUrl = $"{Request.Scheme}://{Request.Host}{relativePath}";

            return Ok(new
            {
                gameId,
                gameUrl,
                gameImageUrl
            });
        }

    }
}
