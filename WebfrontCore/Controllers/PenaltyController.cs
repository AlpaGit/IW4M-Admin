﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFPenalty;

namespace WebfrontCore.Controllers
{
    public class PenaltyController : BaseController
    {
        public PenaltyController(IManager manager) : base(manager)
        {

        }

        public IActionResult List(PenaltyType showOnly = PenaltyType.Any, bool hideAutomatedPenalties = true)
        {
            ViewBag.Description = "List of all the recent penalties (bans, kicks, warnings) on IW4MAdmin";
            ViewBag.Title = Localization["WEBFRONT_PENALTY_TITLE"];
            ViewBag.Keywords = "IW4MAdmin, penalties, ban, kick, warns";
            ViewBag.HideAutomatedPenalties = hideAutomatedPenalties;

            return View(showOnly);
        }

        public async Task<IActionResult> ListAsync(int offset = 0, PenaltyType showOnly = PenaltyType.Any, bool hideAutomatedPenalties = true)
        {
            return await Task.FromResult(View("_List", new ViewModels.PenaltyFilterInfo()
            {
                Offset = offset,
                ShowOnly = showOnly,
                IgnoreAutomated = hideAutomatedPenalties
            }));
        }

        /// <summary>
        /// retrieves all permanent bans ordered by ban date
        /// if request is authorized, it will include the client's ip address.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> PublicAsync()
        {
            IList<PenaltyInfo> penalties;

            using (var ctx = new DatabaseContext(disableTracking: true))
            {
                var iqPenalties = ctx.Penalties
                    .AsNoTracking()
                    .Where(p => p.Type == PenaltyType.Ban && p.Active)
                    .OrderByDescending(_penalty => _penalty.When)
                    .Select(p => new PenaltyInfo()
                    {
                        Id = p.PenaltyId,
                        OffenderId = p.OffenderId,
                        OffenderName = p.Offender.CurrentAlias.Name,
                        OffenderNetworkId = (ulong)p.Offender.NetworkId,
                        OffenderIPAddress = Authorized ? p.Offender.CurrentAlias.IPAddress.ConvertIPtoString() : null,
                        Offense = p.Offense,
                        PunisherId = p.PunisherId,
                        PunisherNetworkId = (ulong)p.Punisher.NetworkId,
                        PunisherName = p.Punisher.CurrentAlias.Name,
                        PunisherIPAddress = Authorized ? p.Punisher.CurrentAlias.IPAddress.ConvertIPtoString() : null,
                        TimePunished = p.When,
                        AutomatedOffense = Authorized ? p.AutomatedOffense : null,
                    });
#if DEBUG == true
                var querySql = iqPenalties.ToSql();
#endif

                penalties = await iqPenalties.ToListAsync();
            }

            return Json(penalties);
        }
    }
}
