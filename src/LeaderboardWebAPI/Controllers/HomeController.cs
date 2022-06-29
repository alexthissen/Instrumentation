﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LeaderboardWebAPI.Controllers
{
    //[SwaggerIgnore]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return new RedirectResult("~/openapi");
        }
    }
}
