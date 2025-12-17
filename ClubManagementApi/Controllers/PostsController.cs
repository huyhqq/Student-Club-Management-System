using ClubManagementApi.Models;
using ClubManagementApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ClubManagementApi.Controllers
{
    [Route("api/posts")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly StudentClubContext _context;
        private readonly CloudinaryService _cloudinaryService;

        public PostsController(StudentClubContext context, CloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        private int CurrentUserId => GetUserIdFromHttpContext(HttpContext);
        public static int GetUserIdFromHttpContext(HttpContext? context, ILogger? logger = null)
        {
            if (context == null)
            {
                logger?.LogError("HttpContext is null in JwtHelper.GetUserIdFromHttpContext.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            if (context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var userIdClaim = context.User.FindFirst("UserId")?.Value
                               ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogInformation("Successfully retrieved UserId {UserId} from claims.", userId);
                    return userId;
                }
                logger?.LogError("Invalid UserId claim: {UserIdClaim}", userIdClaim);
                throw new UnauthorizedAccessException("ID người dùng không hợp lệ.");
            }

            var token = context.Request.Query["access_token"].ToString();
            if (string.IsNullOrWhiteSpace(token))
            {
                logger?.LogError("No access_token found in query parameters.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    logger?.LogError("UserId or nameidentifier claim not found in token.");
                    throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
                }

                if (!int.TryParse(userIdClaim, out var userId))
                {
                    logger?.LogError("Invalid UserId format in token: {UserIdClaim}", userIdClaim);
                    throw new UnauthorizedAccessException($"ID người dùng không hợp lệ: {userIdClaim}");
                }

                logger?.LogInformation("Successfully retrieved UserId {UserId} from access_token.", userId);
                return userId;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to parse JWT token from access_token.");
                throw new UnauthorizedAccessException("Bạn chưa đăng nhập.");
            }
        }

        public class PaginationParams
        {
            [Range(1, int.MaxValue, ErrorMessage = "Số trang phải lớn hơn 0")]
            public int PageNumber { get; set; } = 1;

            [Range(1, 100, ErrorMessage = "Kích thước trang từ 1 đến 100")]
            public int PageSize { get; set; } = 10;

            public string? Search { get; set; }

            public string? SortBy { get; set; } 
            public string? SortOrder { get; set; } 
        }

        public class CreatePostDto
        {
            public int? ClubId { get; set; }

            [StringLength(5000)]
            public string? Content { get; set; }

            [Required(ErrorMessage = "Visibility là bắt buộc")]
            [RegularExpression("^(Public|Members)$", ErrorMessage = "Visibility chỉ có thể là 'Public' hoặc 'Members'")]
            public string Visibility { get; set; } = string.Empty;
        }

        public class UpdatePostDto
        {
            [StringLength(5000)]
            public string? Content { get; set; }
        }

        public class PostPublicDto
        {
            public int PostId { get; set; }
            public int UserId { get; set; }
            public string UserFullName { get; set; } = string.Empty;
            public int? ClubId { get; set; }
            public string? ClubName { get; set; }
            public string? Content { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string Visibility { get; set; } = string.Empty;
            public List<PostImageDto> Images { get; set; } = new();
        }

        public class PostDetailDto
        {
            public int PostId { get; set; }
            public int UserId { get; set; }
            public string UserFullName { get; set; } = string.Empty;
            public int? ClubId { get; set; }
            public string? ClubName { get; set; }
            public string? Content { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public string Visibility { get; set; } = string.Empty;
            public List<PostImageDto> Images { get; set; } = new();
        }

        public class PostImageDto
        {
            public int ImageId { get; set; }
            public string ImageUrl { get; set; } = string.Empty;
            public string? Caption { get; set; }
        }

        // ==================== HÀM HỖ TRỢ ====================

        private IActionResult ValidationErrorResponse()
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .Select(x => new { Field = x.Key, Message = x.Value?.Errors.First().ErrorMessage })
                .ToList();

            return BadRequest(ApiResponse<object>.FailResponse("Dữ liệu không hợp lệ", errors));
        }

        // ==================== CÁC ENDPOINT ====================

        [AllowAnonymous]
        [HttpGet("public")]
        public async Task<IActionResult> GetPublicList([FromQuery] PaginationParams p, [FromQuery] int? clubId = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var query = _context.Posts
                .Include(post => post.User)
                .Include(post => post.Club)
                .Include(post => post.PostImages)
                .Where(post => post.Visibility == "Public" ||
                               (post.Visibility == "Members" && post.Club != null && post.Club.Status == "Active"));

            if (clubId.HasValue)
            {
                query = query.Where(post => post.ClubId == clubId.Value && post.Club != null && post.Club.Status == "Active");
            }

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(post => post.Content != null && post.Content.Contains(p.Search.Trim()));

            // Sort đơn giản
            query = (p.SortBy?.ToLower(), p.SortOrder?.ToLower()) switch
            {
                ("clubname", "asc") => query.OrderBy(post => post.Club != null ? post.Club.ClubName : ""),
                ("clubname", "desc") => query.OrderByDescending(post => post.Club != null ? post.Club.ClubName : ""),
                _ => query.OrderByDescending(post => post.CreatedAt)
            };

            var total = await query.CountAsync();

            var posts = await query
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(post => new PostPublicDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserFullName = post.User.FullName,
                    ClubId = post.ClubId,
                    ClubName = post.Club != null ? post.Club.ClubName : null,
                    Content = post.Content,
                    CreatedAt = post.CreatedAt!.Value.ConvertToVietnamTime(),
                    Visibility = post.Visibility!,
                    Images = post.PostImages.Select(img => new PostImageDto
                    {
                        ImageId = img.ImageId,
                        ImageUrl = img.ImageUrl,
                        Caption = img.Caption
                    }).ToList()
                })
                .ToListAsync();

            return Ok(ApiResponse<List<PostPublicDto>>.SuccessResponse(posts, "Danh sách bài viết công khai", total));
        }

        [AllowAnonymous]
        [HttpGet("public/{id}")]
        public async Task<IActionResult> GetPublicById(int id)
        {
            var post = await _context.Posts
                .Include(post => post.User)
                .Include(post => post.Club)
                .Include(post => post.PostImages)
                .FirstOrDefaultAsync(post => post.PostId == id &&
                    (post.Visibility == "Public" ||
                     (post.Visibility == "Members" && post.Club != null && post.Club.Status == "Active")));

            if (post == null)
                return NotFound(ApiResponse<object>.FailResponse("Bài viết không tồn tại hoặc không công khai"));

            var dto = new PostPublicDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserFullName = post.User.FullName,
                ClubId = post.ClubId,
                ClubName = post.Club != null ? post.Club.ClubName : null,
                Content = post.Content,
                CreatedAt = post.CreatedAt!.Value.ConvertToVietnamTime(),
                UpdatedAt = post.UpdatedAt?.ConvertToVietnamTime(),
                Visibility = post.Visibility!,
                Images = post.PostImages.Select(img => new PostImageDto
                {
                    ImageId = img.ImageId,
                    ImageUrl = img.ImageUrl,
                    Caption = img.Caption
                }).ToList()
            };

            return Ok(ApiResponse<PostPublicDto>.SuccessResponse(dto));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PaginationParams p, [FromQuery] int? clubId = null, [FromQuery] string? visibility = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var query = _context.Posts
                .Include(post => post.User)
                .Include(post => post.Club)
                .Include(post => post.PostImages)
                .AsQueryable();

            if (!User.IsInRole("Admin"))
            {
                query = query.Where(post =>
                    post.UserId == CurrentUserId ||
                    post.Visibility == "Public" ||
                    (post.Visibility == "Members" && post.ClubId.HasValue &&
                     _context.ClubMembers.Any(m => m.ClubId == post.ClubId && m.UserId == CurrentUserId && m.Status == "Approved"))
                );
            }

            if (clubId.HasValue)
            {
                query = query.Where(post => post.ClubId == clubId.Value);

                if (!User.IsInRole("Admin") && !await _context.ClubMembers.AnyAsync(m => m.ClubId == clubId.Value && m.UserId == CurrentUserId && m.Status == "Approved"))
                    return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem bài viết của CLB này"));
            }

            if (!string.IsNullOrEmpty(visibility))
                query = query.Where(post => post.Visibility == visibility);

            if (!string.IsNullOrEmpty(p.Search))
                query = query.Where(post => post.Content != null && post.Content.Contains(p.Search.Trim()));

            query = (p.SortBy?.ToLower(), p.SortOrder?.ToLower()) switch
            {
                ("clubname", "asc") => query.OrderBy(post => post.Club != null ? post.Club.ClubName : ""),
                ("clubname", "desc") => query.OrderByDescending(post => post.Club != null ? post.Club.ClubName : ""),
                _ => query.OrderByDescending(post => post.CreatedAt)
            };

            var total = await query.CountAsync();

            var posts = await query
                .Skip((p.PageNumber - 1) * p.PageSize)
                .Take(p.PageSize)
                .Select(post => new PostDetailDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserFullName = post.User.FullName,
                    ClubId = post.ClubId,
                    ClubName = post.Club != null ? post.Club.ClubName : null,
                    Content = post.Content,
                    CreatedAt = post.CreatedAt!.Value.ConvertToVietnamTime(),
                    Visibility = post.Visibility!,
                    Images = post.PostImages.Select(img => new PostImageDto
                    {
                        ImageId = img.ImageId,
                        ImageUrl = img.ImageUrl,
                        Caption = img.Caption
                    }).ToList()
                })
                .ToListAsync();

            return Ok(ApiResponse<List<PostDetailDto>>.SuccessResponse(posts, "Danh sách bài viết", total));
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _context.Posts
                .Include(post => post.User)
                .Include(post => post.Club)
                .Include(post => post.PostImages)
                .FirstOrDefaultAsync(post => post.PostId == id);

            if (post == null) return NotFound(ApiResponse<object>.FailResponse("Bài viết không tồn tại"));

            bool canView = User.IsInRole("Admin") ||
                           post.UserId == CurrentUserId ||
                           post.Visibility == "Public" ||
                           (post.Visibility == "Members" && post.ClubId.HasValue &&
                            await _context.ClubMembers.AnyAsync(m => m.ClubId == post.ClubId && m.UserId == CurrentUserId && m.Status == "Approved"));

            if (!canView)
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không có quyền xem bài viết này"));

            var dto = new PostDetailDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserFullName = post.User.FullName,
                ClubId = post.ClubId,
                ClubName = post.Club != null ? post.Club.ClubName : null,
                Content = post.Content,
                CreatedAt = post.CreatedAt!.Value.ConvertToVietnamTime(),
                UpdatedAt = post.UpdatedAt?.ConvertToVietnamTime(),
                Visibility = post.Visibility!,
                Images = post.PostImages.Select(img => new PostImageDto
                {
                    ImageId = img.ImageId,
                    ImageUrl = img.ImageUrl,
                    Caption = img.Caption
                }).ToList()
            };

            return Ok(ApiResponse<PostDetailDto>.SuccessResponse(dto));
        }

        [Authorize(Roles = "Student,ClubLeader")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreatePostDto dto, [FromForm] List<IFormFile>? images = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            if (dto.ClubId.HasValue)
            {
                var club = await _context.Clubs.FirstOrDefaultAsync(c => c.ClubId == dto.ClubId.Value && c.Status == "Active");
                if (club == null)
                    return BadRequest(ApiResponse<object>.FailResponse("CLB không tồn tại hoặc chưa hoạt động"));

                var isMember = await _context.ClubMembers.AnyAsync(m => m.ClubId == dto.ClubId.Value && m.UserId == CurrentUserId && m.Status == "Approved");
                if (!isMember)
                    return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không phải thành viên của CLB này"));
            }

            if (dto.Visibility == "Members" && !dto.ClubId.HasValue)
                return BadRequest(ApiResponse<object>.FailResponse("Visibility 'Members' yêu cầu phải có ClubId"));

            var post = new Post
            {
                UserId = CurrentUserId,
                ClubId = dto.ClubId,
                Content = dto.Content?.Trim(),
                CreatedAt = TimeZoneHelper.NowInVietnam,
                UpdatedAt = TimeZoneHelper.NowInVietnam,
                Visibility = dto.Visibility
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Posts.Add(post);
                await _context.SaveChangesAsync();

                if (images != null && images.Count > 0)
                {
                    foreach (var image in images)
                    {
                        if (image.Length > 0)
                        {
                            var imageUrl = await _cloudinaryService.UploadAsync(image, "club-posts");
                            _context.PostImages.Add(new PostImage
                            {
                                PostId = post.PostId,
                                ImageUrl = imageUrl,
                                CreatedAt = TimeZoneHelper.NowInVietnam
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            if (dto.Visibility == "Members" && dto.ClubId.HasValue)
            {
                var clubName = await _context.Clubs
                    .Where(c => c.ClubId == dto.ClubId.Value)
                    .Select(c => c.ClubName)
                    .FirstOrDefaultAsync();

                FireAndForget(async () =>
                {
                    var members = await _context.ClubMembers
                        .Where(m => m.ClubId == dto.ClubId.Value && m.Status == "Approved" && m.UserId != CurrentUserId)
                        .Select(m => m.UserId)
                        .ToListAsync();

                    if (members.Any())
                    {
                        await NotificationService.SendToManyAsync(members, "Bài viết mới trong CLB", $"Có bài viết mới trong CLB {clubName}");
                    }
                });
            }

            return Ok(ApiResponse<object>.SuccessResponse(new { post.PostId }, "Đăng bài viết thành công"));
        }

        [Authorize]
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdatePostDto dto, [FromForm] List<IFormFile>? newImages = null)
        {
            if (!ModelState.IsValid) return ValidationErrorResponse();

            var post = await _context.Posts
                .Include(p => p.PostImages)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null) return NotFound(ApiResponse<object>.FailResponse("Bài viết không tồn tại"));

            bool isOwner = post.UserId == CurrentUserId;
            if (!isOwner && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không phải chủ bài viết này"));

            post.Content = dto.Content?.Trim();
            post.UpdatedAt = TimeZoneHelper.NowInVietnam;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (newImages != null && newImages.Count > 0)
                {
                    foreach (var image in newImages)
                    {
                        if (image.Length > 0)
                        {
                            var imageUrl = await _cloudinaryService.UploadAsync(image, "club-posts");
                            _context.PostImages.Add(new PostImage
                            {
                                PostId = post.PostId,
                                ImageUrl = imageUrl,
                                CreatedAt = TimeZoneHelper.NowInVietnam
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(ApiResponse<string>.SuccessResponse(null, "Cập nhật bài viết thành công"));
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts
                .Include(p => p.PostImages)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null) return NotFound(ApiResponse<object>.FailResponse("Bài viết không tồn tại"));

            bool isOwner = post.UserId == CurrentUserId;
            if (!isOwner && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không phải chủ bài viết này"));

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.PostImages.RemoveRange(post.PostImages);
                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return Ok(ApiResponse<string>.SuccessResponse(null, "Xóa bài viết thành công"));
        }

        [Authorize]
        [HttpDelete("{postId}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int postId, int imageId)
        {
            var post = await _context.Posts
                .Include(p => p.PostImages)
                .FirstOrDefaultAsync(p => p.PostId == postId);

            if (post == null) return NotFound(ApiResponse<object>.FailResponse("Bài viết không tồn tại"));

            bool isOwner = post.UserId == CurrentUserId;
            if (!isOwner && !User.IsInRole("Admin"))
                return StatusCode(403, ApiResponse<object>.FailResponse("Bạn không phải chủ bài viết này"));

            var image = post.PostImages.FirstOrDefault(img => img.ImageId == imageId);
            if (image == null) return NotFound(ApiResponse<object>.FailResponse("Ảnh không tồn tại"));

            _context.PostImages.Remove(image);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.SuccessResponse(null, "Xóa ảnh thành công"));
        }

        private static void FireAndForget(Func<Task> taskFunc)
        {
            Task.Run(async () =>
            {
                try
                {
                    await taskFunc();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"FireAndForget error: {ex}");
                }
            });
        }
    }
}