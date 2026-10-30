using CertTool.Backend.LetsEncrypt.Cryptography;
using CertTool.Backend.LetsEncrypt.IO;
using Newtonsoft.Json;
using CertTool.Backend.LetsEncrypt.Jws;

namespace CertTool.Backend.LetsEncrypt.Entities
{
    public partial class Account
    {

        [JsonIgnore] public RsaKeyPair Key { get; private set; } = null!;

        [JsonIgnore] public JwsSigner Signer { get; private set; } = null!;

        // Ctors

        public Account()
        {
            TermsOfServiceAgreed = true;
        }

        public Account(RsaKeyPair key)
            : this()
        {
            Key = key;
            Signer = new JwsSigner(Key);
        }

        public Account(RsaKeyPair key, string location)
            : this(key)
        {
            Location = new Uri(location);
        }

        // Public Methods

        public void FillBy(CertTool.Backend.LetsEncrypt.Entities.Account account)
        {
            this.UnknownContent = account.UnknownContent;
            this.Location = account.Location;
            this.Error = account.Error;
            this.Status = account.Status;
            this.Contact = account.Contact;
            this.TermsOfServiceAgreed = account.TermsOfServiceAgreed;
            this.InitialIp = account.InitialIp;
            this.CreatedAt = account.CreatedAt;
        }
        
        // Static Methods
        
        public static Account Create(List<string> contactEmails)
        {
            var key = RsaKeyPair.New();
        
            return new Account(key)
            {
                Contact = contactEmails
            };
        }
    }
}