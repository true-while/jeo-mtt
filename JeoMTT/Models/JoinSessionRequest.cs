namespace JeoMTT.Models
{
    /// <summary>
    /// Request model for joining a game session
    /// </summary>
    public class JoinSessionRequest
    {
        public string JoinCode { get; set; } = string.Empty;
        public string PlayerNickname { get; set; } = string.Empty;
    }
}
