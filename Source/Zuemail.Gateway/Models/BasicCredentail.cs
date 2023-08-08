namespace Zuemail.Gateway.Models
{
    public sealed class BasicCredentail
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public override string ToString() => Username;
    }
}
