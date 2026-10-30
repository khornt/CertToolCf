using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CertTool.Backend.LetsEncrypt.Entities;

namespace CertTool.Models.Dto
{
    public class ChallengeStore
    {
        public List<Challenge> Challenges { get; set; } = new List<Challenge>();
        public Account? Account { get; set; }
        public Order? Order { get; set; }
        
    }
}
