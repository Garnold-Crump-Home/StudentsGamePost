namespace AccountsManager.Models
{
    public class games
    {
        public int GameID { get; set; }

        public DateTime? GameCreationDate { get; set; } = DateTime.UtcNow;

        public string GameName { get; set; }

        public int PlayersViews { get; set; }

        public string? uploads { get; set; }

        public override string ToString()
        {
            return $"{GameName} {GameCreationDate}";
        }
    }
}
