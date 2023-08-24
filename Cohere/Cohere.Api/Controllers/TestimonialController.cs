using Cohere.Api.Utils;
using Cohere.Domain.Models.ModelsAuxiliary;
using Cohere.Domain.Models.Testimonial;
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
    public class TestimonialController : CohereController
    {
        private readonly ITestimonialService _testimonialService;

        public TestimonialController(ITestimonialService testimonialService)
        {
            _testimonialService = testimonialService;
        }

        [HttpPost]
        public async Task<IActionResult> Add(TestimonialViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var result = await _testimonialService.Insert(model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var result = await _testimonialService.Get(id);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] TestimonialViewModel model)
        {
            if (model == null)
            {
                return BadRequest();
            }

            var result = await _testimonialService.Update(id, model);

            if (!result.Succeeded)
            {
                return BadRequest(new ErrorInfo(result.Message));
            }

            return Ok(result.Payload);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var result = await _testimonialService.Delete(id);

            if (result.Succeeded)
            {
                return Ok();
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

            var result = await _testimonialService.GetByContributionId(contributionId);

            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }

        [HttpPatch("ToggleShowcase")]
        public async Task<IActionResult> ToggleShowcase(string id)
        {
            var result = await _testimonialService.ToggleShowcase(id);
            if (result.Succeeded)
            {
                return Ok(result.Payload);
            }

            return BadRequest(new ErrorInfo { Message = result.Message });
        }
    }
}
