using Cohere.Api.Utils;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Pods;
using Cohere.Domain.Service.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cohere.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PodController : CohereController
    {
        private readonly IPodService _podService;

        public PodController(IPodService podService)
        {
            _podService = podService;
        }

        [HttpGet]
        [Authorize(Policy = "IsScalePaidTierPolicy")]
        public async Task<IActionResult> GetByUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest();
            }

            var result = await _podService.GetByUserId(userId);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpGet("GetByContributionId")]
        public async Task<IActionResult> GetByContributionId(string contributionId)
        {
            if (string.IsNullOrEmpty(contributionId))
            {
                return BadRequest();
            }

            var result = await _podService.GetByContributionId(contributionId);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPost]
        [Authorize(Policy = "IsScalePaidTierPolicy")]
        public async Task<IActionResult> Add(PodViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var result = await _podService.Insert(model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "IsScalePaidTierPolicy")]
        public async Task<IActionResult> Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var result = await _podService.Get(id);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "IsScalePaidTierPolicy")]
        public async Task<IActionResult> Put(string id, [FromBody] PodViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var result = await _podService.Update(id, model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "IsScalePaidTierPolicy")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var result = await _podService.Delete(id);

            if (result.Succeeded)
            {
                return Ok();
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }
    }
}
