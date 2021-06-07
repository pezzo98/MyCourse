using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyCourse.Models.Exceptions;
using MyCourse.Models.Options;
using MyCourse.Models.Services.Infrastructure;
using MyCourse.Models.ViewModels;

namespace MyCourse.Models.Services.Application
{
    public class EfCoreCourseService : ICourseService
    {
        private readonly ILogger<EfCoreCourseService> logger;
        private readonly MyCourseDbContext dbContext;
        private readonly IOptionsMonitor<CoursesOptions> coursesOptions;

        public EfCoreCourseService(ILogger<EfCoreCourseService> logger, MyCourseDbContext dbContext, IOptionsMonitor<CoursesOptions> coursesOptions)
        {
            this.coursesOptions = coursesOptions;
            this.logger = logger;
            this.dbContext = dbContext;
        }

        public async Task<CourseDetailViewModel> GetCourseAsync(int id)
        {
            IQueryable<CourseDetailViewModel> queryLinq = dbContext.Courses
                .Include(course => course.Lessons)
                .Where(course => course.Id == id)
                .Select(course => CourseDetailViewModel.FromEntity(course)); //Usando metodi statici come FromEntity, la query potrebbe essere inefficiente. Mantenere il mapping nella lambda oppure usare un extension method personalizzato
            CourseDetailViewModel viewModel = await queryLinq.FirstOrDefaultAsync();
            if (viewModel == null)
            {
                logger.LogWarning("Course {id} not found", id);
                throw new CourseNotFoundException(id);
            }
            CourseDetailViewModel viewModel = await queryLinq.SingleAsync();

            return viewModel;
        }

        public async Task<List<CourseViewModel>> GetCoursesAsync()
        {
            IQueryable<CourseViewModel> queryLinq = dbContext.Courses
                .AsNoTracking()
                .Select(course => CourseViewModel.FromEntity(course));

            List<CourseViewModel> courses = await queryLinq.ToListAsync(); //La query al database viene inviata qui, quando manifestiamo l'intenzione di voler leggere i risultati

            return courses;
        }
    }
}