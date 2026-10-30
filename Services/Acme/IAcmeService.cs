using CertTool.Models.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertTool.Backend.LetsEncrypt.Entities;

namespace CertTool.Services.Acme
{
    public interface IAcmeService
    {
        Task<ChallengeStore> CreateChallengeAsync(string fqdn, string email, string accountPassphrase);

        Task<bool> ValidateChallengeAsync(ChallengeStore challengeStore);

        Task<Certificate> GenerateCertificateAsync(Account account, Order order, string certificateCommonName);
        
    }
}
