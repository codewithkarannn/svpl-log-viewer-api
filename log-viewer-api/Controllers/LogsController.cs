using System.Net;
using log_viewer_api.Interfaces;
using log_viewer_api.Models;
using log_viewer_api.Services;
using Microsoft.AspNetCore.Mvc;
using MyApp.Models;

namespace log_viewer_api.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class LogsController(ILogFileService logFileService) : ControllerBase
{

    [HttpGet]
    public ActionResult<ApiResponse<List<LogFileDto>>> GetFiles([FromQuery]string rootDirectory)
    {
        rootDirectory = WebUtility.UrlDecode(rootDirectory);
        var files = logFileService.GetLogFiles(rootDirectory);
        if (files.Count <= 0)
        {
            // Standardized Error
            return NotFound(ApiResponse<List<LogFileDto>>.Fail($"No files found for {rootDirectory}"));
        }
        return Ok(ApiResponse<List<LogFileDto>>.Success(files, "Files retrieved successfully."));
    }

    [HttpGet]
    public IActionResult HealthCheck()
    {
        return Ok(ApiResponse<object>.Success( "Healthy",  "Healthy"  ));
  
    }

    [HttpGet]
    public async Task< ActionResult<ApiResponse<List<LogEntryDto>>>> ReadFile([FromQuery] string rootDirectory)
    {
   

        var logs = await logFileService.ReadFile(rootDirectory);
        if (logs.Count <= 0)
        {
            // Standardized Error
            return NotFound(ApiResponse<List<LogFileDto>>.Fail($"No files found for {rootDirectory}"));
        }
        
        return Ok(ApiResponse<List<LogEntryDto>>.Success(logs, "Files read successfully."));
    }
}