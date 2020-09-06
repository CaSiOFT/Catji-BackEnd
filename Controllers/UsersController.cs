﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Claims;
using System.Threading.Tasks;
using CatjiApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CatjiApi.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ModelContext _context;

        public UsersController(ModelContext context)
        {
            _context = context;
        }

        [Serializable]
        public class UserDTO
        {
            public int usid;
            public string nickname;
            public string password;
            public string email;
            public string phone;
        }
        //GET:api/Users/search 根据关键词查询用户信息
        [HttpGet("search")]
        public async Task<IActionResult> GetUsersearch(int page, string keyword, bool only_cat)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }
            if (only_cat == true)
            {
                var keys = _context.Users.Where(x => x.Signature.Contains(keyword) && x.CatId != 0).Skip(page);
                if (await keys.CountAsync() == 0)
                {
                    return NotFound(new { status = "未能找到相关用户" });
                }

                else
                {
                    var result = keys.Select(x => new

                    {
                        status = "ok",
                        data = new
                        {
                            Vid = x.Usid,
                            name = x.Nickname,
                            desc = x.Signature,
                            follow_num = x.FollowerNum,
                            avatar = x.Avatar

                        }
                    });
                    return Ok(new { status = "ok", data = result });
                }
            }
            else
            {
                var keys = _context.Users.Where(x => x.Signature.Contains(keyword)).Skip(page);
                if (await keys.CountAsync() == 0)
                {
                    return NotFound(new { status = "未能找到相关用户" });
                }

                else
                {
                    var result = keys.Select(x => new

                    {
                        status = "ok",
                        data = new
                        {
                            Vid = x.Usid,
                            name = x.Nickname,
                            desc = x.Signature,
                            follow_num = x.FollowerNum,
                            avatar = x.Avatar

                        }
                    });
                    return Ok(new { status = "ok", data = result });
                }
            }
        }

        // POST: /api/users/register 注册
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserDTO user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }

            if (user.email is null && user.phone is null)
            {
                return BadRequest(new { status = "bad request" });
            }

            var findUser = _context.Users.Where(x =>
                user.phone != null && x.Tel == user.phone ||
                user.email != null && x.Email == user.email ||
                x.Nickname == user.nickname
            );

            if (findUser.Count() != 0)
            {
                return BadRequest(new { status = "replicated" });
            }

            var user0 = new Users();
            user0.Nickname = user.nickname;
            user0.Tel = user.phone;
            user0.Email = user.email;
            user0.Password = user.password;
            user0.CreateTime = DateTime.Now;
            user0.ChangedTime = DateTime.Now;

            try
            {
                _context.Users.Add(user0);
                //_context.SaveChanges();
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                return BadRequest(new { status = "Create failed.", data = e.ToString() });
            }

            await RealLogin(user, user0);

            return Ok(new { status = "ok", data = new { usid = user0.Usid } });
        }

        // POST: /api/users/login 登录
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserDTO user)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }

            var auth = await HttpContext.AuthenticateAsync();
            if (auth.Succeeded)
            {
                return BadRequest(new { status = "already login" });
            }

            if (user.email == null && user.phone == null && user.nickname == null)
            {
                return BadRequest(new { status = "bad request" });
            }

            var findUser = _context.Users.Where(x =>
                (user.phone != null && x.Tel == user.phone ||
                user.email != null && x.Email == user.email ||
                user.nickname != null && x.Nickname == user.nickname) &&
                user.password != null && x.Password == user.password
            ).FirstOrDefault();

            if (findUser == null)
            {
                return NotFound(new { status = "not found" });
            }

            await RealLogin(user, findUser);

            return Ok(new { status = "ok" });
        }

        // 这里必须返回Task不然调用的时候没法await
        // 参数分别为传输用的user对象
        // 和数据库存储的user对象
        private async Task RealLogin(UserDTO user, Users userDAO)
        {
            var claims = new List<Claim> {
                new Claim ("User", userDAO.Usid.ToString()),
                new Claim ("LastChanged", userDAO.ChangedTime.ToTimestamp().ToString())
            };

            //使用证件单元创建一张身份证
            var identity = new ClaimsIdentity(claims);

            //使用身份证创建一个证件当事人
            var identityPrincipal = new ClaimsPrincipal(identity);

            //登录
            await HttpContext.SignInAsync(identityPrincipal, new AuthenticationProperties
            {
                //ExpiresUtc = DateTime.UtcNow.AddDays(1),
                IsPersistent = true,
                AllowRefresh = true
            });
        }

        // POST: /api/users/logout 注销
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var auth = await HttpContext.AuthenticateAsync();
            if (!auth.Succeeded)
            {
                return NotFound(new { status = "not login" });
            }
            await HttpContext.SignOutAsync();
            return Ok(new { status = "ok" });
        }

        // GET: /api/users 查询登录信息(!不是用户信息)
        [HttpGet]
        public async Task<IActionResult> LoginInfo()
        {
            var auth = await HttpContext.AuthenticateAsync();
            if (!auth.Succeeded)
            {
                return NotFound(new { status = "not login" });
            }

            var claim = User.FindFirstValue("User");
            int usid;

            if (!Int32.TryParse(claim, out usid))
            {
                return BadRequest(new { status = "validation failed" });
            }

            var user = await _context.Users.FindAsync(usid);

            return Ok(new
            {
                status = "ok",
                data = new
                {
                    usid = user.Usid,
                    nickname = user.Nickname,
                    password = user.Password,
                    email = user.Email,
                    phone = user.Tel,
                }
            });
        }

        // 获取用户排行榜
        [HttpGet("hotlist")]
        public IActionResult GetUTop()
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }

            var user_top = _context.Users.OrderByDescending(x => x.FollowerNum + x.Video.Count()).Take(10);

            var result = user_top.Select(x => new
            {
                usid = x.Usid,
                avatar = x.Avatar,
                name = x.Nickname,
                upload_num = _context.Video.Where(y => y.Usid == x.Usid).Count(),
                fan_num = x.FollowerNum
            });

            return Ok(new { status = "ok", data = result });
        }

        // GET: /api/users/info 查询用户信息(!不是登录信息)
        [HttpGet("info")]
        public async Task<IActionResult> GetUserInfo(int? usid)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }

            // var auth = await HttpContext.AuthenticateAsync();
            // if (!auth.Succeeded)
            // {
            //     return BadRequest(new { status = "not login" });
            // }

            // int usid;
            // bool bo1 = int.TryParse(auth.Principal.Identity.Name, out usid);

            if (usid == null)
            {
                return BadRequest(new { status = "no usid" });
            }

            var users = await _context.Users.FindAsync(usid);

            if (users == null)
            {
                return NotFound(new { status = "not found" });
            }

            string baseUrl = Request.Scheme + "://" + Request.Host + "/";

            return Ok(new
            {
                status = "ok",
                data = new
                {
                    usid = users.Usid,
                    nickname = users.Nickname,
                    gender = users.Gender,
                    avatar = baseUrl + "images/" + (users.Avatar != null ? users.Avatar : "noface.png"),
                    signature = users.Signature,
                    follower_num = users.FollowerNum,
                    followee_num = users.FollowUs.Count,
                    upload_num = users.Video.Count,
                    birthday = Extensionmethods.ToTimestamp(users.Birthday),
                    cat_id = users.CatId
                }
            });
        }

        // POST: /api/users/updateinfo
        [HttpPost("updateinfo")]
        public async Task<IActionResult> updateinfo(IFormCollection paras)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { status = "invalid", data = ModelState });
            }
            var auth = await HttpContext.AuthenticateAsync();
            if (!auth.Succeeded)
            {
                return NotFound(new { status = "not login" });
            }

            var claim = User.FindFirstValue("User");

            if (!Int32.TryParse(claim, out var loginUsid))
            {
                return BadRequest(new { status = "validation failed" });
            }

            var user = await _context.Users.FindAsync(loginUsid);

            if (user == null)
            {
                return BadRequest(new { status = "No user!" });
            }

            if (paras.TryGetValue("email", out var paramEmail) && paramEmail != user.Email)
            {
                var temp_u = _context.Users.Where(x => x.Email == paramEmail);

                if (temp_u.Count() != 0)
                {
                    return BadRequest(new { status = "The Email address is already taken" });
                }

                user.Email = paramEmail;
            }

            if (paras.TryGetValue("tel", out var paramTel) && paramTel != user.Tel)
            {
                var temp_u = _context.Users.Where(x => x.Tel == paramTel);

                if (temp_u.Count() != 0)
                {
                    return BadRequest(new { status = "The Tel is already used" });
                }

                user.Tel = paramTel;
            }

            if (paras.TryGetValue("nickname", out var paramNickname) && paramNickname != user.Nickname)
            {
                var temp_u = _context.Users.Where(x => x.Nickname == paramNickname);

                if (temp_u.Count() != 0)
                {
                    return BadRequest(new { status = "The Nickname is already taken" });
                }

                user.Nickname = paramNickname;
            }

            if (paras.TryGetValue("password", out var paramPassword))
            {
                user.Password = paramPassword;
            }

            if (paras.TryGetValue("gender", out var paramGender))
            {
                user.Gender = paramGender;
            }

            if (paras.TryGetValue("birthday", out var paramBirthday))
            {
                try
                {
                    var convertedDate = Convert.ToInt32(paramBirthday);
                    user.Birthday = convertedDate.ToDateTime();
                }
                catch
                {
                    return BadRequest(new { status = "Date format error" });
                }
            }

            if (paras.TryGetValue("signature", out var paramSignature))
            {
                user.Signature = paramSignature;
            }

            IFormFile avatarFile = paras.Files.GetFile("avatar");
            if (avatarFile != null)
            {
                string avatarFileName = Guid.NewGuid().ToString() + '.' + avatarFile.FileName.Split('.').Last();
                string pathToSave = "wwwroot/images" + "/" + avatarFileName;
                using (var stream = System.IO.File.Create(pathToSave))
                {
                    await avatarFile.CopyToAsync(stream);
                }
                user.Avatar = avatarFileName;
            }

            if (paras.TryGetValue("cat_id", out var paramCatId))
            {
                if (!Int32.TryParse(paramCatId, out var cat_id))
                {
                    return BadRequest(new { status = "Birthday format incorrect" });
                }

                var temp_u = _context.Users.Where(x => x.CatId == cat_id);

                if (temp_u.Count() != 0)
                {
                    return BadRequest(new { status = "The Catid is already used" });
                }

                user.CatId = cat_id;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                return NotFound(new { status = "Save failed.", data = e.ToString() });
            }

            return Ok(new { status = "ok" });
        }


        //GET:/api/users/changeavatar

        // [HttpGet("changeavatar")]
        // public async Task<IActionResult> changeavatar(Users us)
        // {
        //     var auth = await HttpContext.AuthenticateAsync();
        //     if (!auth.Succeeded)
        //     {
        //         return NotFound(new { status = "not login" });
        //     }
        //     int uid;
        //     var claim = User.FindFirstValue("User");
        //     if (!Int32.TryParse(claim, out uid))
        //     {
        //         return BadRequest(new { status = "validation failed" });
        //     }
        //     var user = await _context.Users.FindAsync(uid);
        //     try
        //     {
        //         user.Avatar = us.Avatar;
        //         await _context.SaveChangesAsync();
        //     }
        //     catch
        //     {
        //         return NotFound("上传头像失败");
        //     }
        //     return Ok(new { status = "ok" });
        // }

        // // DELETE: api/Users/5 [需要登录] 更新个人信息
        // [HttpDelete("{id}")]
        // public async Task<IActionResult> DeleteUsers([FromRoute] int id)
        // {
        //     if (!ModelState.IsValid)
        //     {
        //         return BadRequest(new { status = "invalid", data = ModelState });
        //     }

        //     var users = await _context.Users.FindAsync(id);
        //     if (users == null)
        //     {
        //         return NotFound(new { status = "not found" });
        //     }

        //     _context.Users.Remove(users);
        //     try
        //     {
        //         await _context.SaveChangesAsync();
        //     }
        //     catch (DbUpdateException e)
        //     {
        //         return NotFound(new { status = "db exception", data = e.ToString() });
        //     }
        //     return Ok(new { status = "ok", data = users });
        // }

        private bool UsersExists(int id)
        {
            return _context.Users.Any(e => e.Usid == id);
        }
    }
}