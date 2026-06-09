using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuchniaUCygana.Application.DTOs.Logistics;

    public sealed class VehicleDto
    {
        public int Id { get; set; }

        public string RegistrationNumber { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public decimal MaxLoadKg { get; set; }

        public string Status { get; set; } = string.Empty;
    }
