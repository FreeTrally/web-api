using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly LinkGenerator _linkGenerator;

        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
        {
            _userRepository = userRepository;
            _mapper = mapper;
            _linkGenerator = linkGenerator;
        }

        [Produces("application/json", "application/xml")]
        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var entity = _userRepository.FindById(userId);

            if (entity == null)
                return NotFound();

            var dto = _mapper.Map<UserDto>(entity);
            return Ok(dto);
        }

        [Produces("application/json", "application/xml")]
        [HttpPost]
        public IActionResult CreateUser([FromBody] CreateUserDto user)
        {
            if (user is {Login: { }} && !user.Login.All(char.IsLetterOrDigit))
                ModelState.AddModelError("Login", "Incorrect login");
            if (!ModelState.IsValid)
            {
                if (ModelState.ContainsKey("Login")
                    && ModelState["Login"].ValidationState == ModelValidationState.Invalid)
                {
                    return UnprocessableEntity(ModelState);
                }
                return BadRequest();
            }

            var entity = _mapper.Map<UserEntity>(user);
            var result = _userRepository.Insert(entity);

            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = result.Id },
                result.Id);
        }

        [Produces("application/json", "application/xml")]
        [HttpPut("{userId}")]
        public IActionResult UpdateUser([FromRoute] Guid userId, [FromBody] UpdateUserDto user)
        {
            if (user is {FirstName: null})
                ModelState.AddModelError("FirstName", "Incorrect first name");
            if (user is {LastName: null})
                ModelState.AddModelError("LastName", "Incorrect last name");
            if (!ModelState.IsValid)
            {
                if (ModelState.ContainsKey("Login")
                    || ModelState.ContainsKey("FirstName")
                    || ModelState.ContainsKey("LastName"))
                {
                    return UnprocessableEntity(ModelState);
                }
                return BadRequest();
            }
            user.Id = userId;
            var entity = _mapper.Map<UserEntity>(user);

            _userRepository.UpdateOrInsert(entity, out var isInserted);

            return !isInserted ? NoContent() 
                : CreatedAtRoute(
                nameof(GetUserById),
                new { userId = entity.Id },
                entity.Id);
        }

        [Produces("application/json", "application/xml")]
        [HttpPatch("{userId}")]
        public IActionResult PartiallyUpdateUser([FromRoute] Guid userId, 
            [FromBody] JsonPatchDocument<UpdateUserDto> patchDoc)
        {
            if (patchDoc == null)
                return BadRequest();
            var user = new UpdateUserDto
            {
                Id = userId
            };
            patchDoc.ApplyTo(user, ModelState);
            TryValidateModel(user);
            if (_userRepository.FindById(userId) == null)
                ModelState.AddModelError("Id", "User not found");
            if (user is { FirstName: null } || user.FirstName.Length == 0)
                ModelState.AddModelError("FirstName", "Incorrect first name");
            if (user is { LastName: null } || user.LastName.Length == 0)
                ModelState.AddModelError("LastName", "Incorrect last name");
            if (!ModelState.IsValid)
            {
                if (ModelState.ContainsKey("Id"))
                {
                    return NotFound();
                }
                if (ModelState.ContainsKey("Login")
                    || ModelState.ContainsKey("FirstName")
                    || ModelState.ContainsKey("LastName"))
                {
                    return UnprocessableEntity(ModelState);
                }
                return BadRequest();
            }
            
            var entity = _mapper.Map<UserEntity>(user);

            _userRepository.Update(entity);

            return NoContent();
        }

        [Produces("application/json", "application/xml")]
        [HttpDelete("{userId}")]
        public IActionResult DeleteUser([FromRoute] Guid userId)
        {
            if (_userRepository.FindById(userId) == null)
                return NotFound();

            _userRepository.Delete(userId);
            return NoContent();
        }

        [Produces("application/json", "application/xml")]
        [HttpGet(Name = nameof(GetUsers))]
        public IActionResult GetUsers([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
        {
            if (pageNumber is null or < 1)
                pageNumber = 1;
            pageSize ??= 10;
            pageSize = Math.Clamp(pageSize.Value, 1, 20);

            var previousPage = _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), 
                new {pageNumber, pageSize});
            var nextPage = _linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers),
                new { pageNumber, pageSize });
            

            var pageList = _userRepository.GetPage(pageNumber.Value, pageSize.Value);
            var users = _mapper.Map<IEnumerable<UserDto>>(pageList);
            var paginationHeader = new
            {
                previousPageLink = pageNumber > 1 ? previousPage : null,
                nextPageLink = nextPage,
                totalCount = pageList.TotalCount,
                pageSize,
                currentPage = pageNumber,
                totalPages = pageList.TotalPages
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
            return Ok(users);
        }

        [Produces("application/json", "application/xml")]
        [HttpOptions]
        public IActionResult UserOptions()
        {
            Response.Headers.Add("Allow", "POST,GET,OPTIONS");
            return Ok();
        }
    }
}