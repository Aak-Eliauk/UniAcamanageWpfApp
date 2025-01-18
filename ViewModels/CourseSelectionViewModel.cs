using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.ObjectModel;  // 用于 ObservableCollection
using System.ComponentModel;           // 用于 INotifyPropertyChanged
using System.Linq;                     // 用于 LINQ 查询

namespace UniAcamanageWpfApp.ViewModels
{
    public class CourseSelectionViewModel : INotifyPropertyChanged
    {
        public CourseSelectionViewModel()
        {
            // 初始化属性
            SelectedCourses = new ObservableCollection<Course>();
            AvailableCourses = new ObservableCollection<Course>();
            RecommendedBasicCourses = new ObservableCollection<Course>();
            RecommendedMajorCourses = new ObservableCollection<Course>();
            RecommendedElectiveCourses = new ObservableCollection<Course>();
        }

        #region 属性


        private ObservableCollection<Course> _selectedCourses;
        public ObservableCollection<Course> SelectedCourses
        {
            get => _selectedCourses;
            set
            {
                _selectedCourses = value;
                OnPropertyChanged(nameof(SelectedCourses));
                UpdateStatistics();
            }
        }

        private ObservableCollection<Course> _availableCourses;
        public ObservableCollection<Course> AvailableCourses
        {
            get => _availableCourses;
            set
            {
                _availableCourses = value;
                OnPropertyChanged(nameof(AvailableCourses));
                OnPropertyChanged(nameof(AvailableCourseCount));
            }
        }

        private ObservableCollection<Course> _recommendedBasicCourses;
        public ObservableCollection<Course> RecommendedBasicCourses
        {
            get => _recommendedBasicCourses;
            set
            {
                _recommendedBasicCourses = value;
                OnPropertyChanged(nameof(RecommendedBasicCourses));
                OnPropertyChanged(nameof(BasicRequiredCount));
            }
        }

        private ObservableCollection<Course> _recommendedMajorCourses;
        public ObservableCollection<Course> RecommendedMajorCourses
        {
            get => _recommendedMajorCourses;
            set
            {
                _recommendedMajorCourses = value;
                OnPropertyChanged(nameof(RecommendedMajorCourses));
                OnPropertyChanged(nameof(MajorRequiredCount));
            }
        }

        private ObservableCollection<Course> _recommendedElectiveCourses;
        public ObservableCollection<Course> RecommendedElectiveCourses
        {
            get => _recommendedElectiveCourses;
            set
            {
                _recommendedElectiveCourses = value;
                OnPropertyChanged(nameof(RecommendedElectiveCourses));
                OnPropertyChanged(nameof(ElectiveCount));
            }
        }

        private decimal _totalCredits;
        public decimal TotalCredits
        {
            get => _totalCredits;
            set
            {
                if (_totalCredits != value)
                {
                    _totalCredits = value;
                    OnPropertyChanged(nameof(TotalCredits));
                }
            }
        }

        private int _selectedCourseCount;
        public int SelectedCourseCount
        {
            get => _selectedCourseCount;
            set
            {
                if (_selectedCourseCount != value)
                {
                    _selectedCourseCount = value;
                    OnPropertyChanged(nameof(SelectedCourseCount));
                }
            }
        }

        public int AvailableCourseCount => AvailableCourses?.Count ?? 0;
        public int BasicRequiredCount => RecommendedBasicCourses?.Count ?? 0;
        public int MajorRequiredCount => RecommendedMajorCourses?.Count ?? 0;
        public int ElectiveCount => RecommendedElectiveCourses?.Count ?? 0;

        private string _currentMajor;
        public string CurrentMajor
        {
            get => _currentMajor;
            set
            {
                _currentMajor = value;
                OnPropertyChanged(nameof(CurrentMajor));
            }
        }

        private DateTime _queryTime;
        public DateTime QueryTime
        {
            get => _queryTime;
            set
            {
                _queryTime = value;
                OnPropertyChanged(nameof(QueryTime));
            }
        }

        #endregion

        #region 方法

        private void UpdateStatistics()
        {
            SelectedCourseCount = SelectedCourses?.Count ?? 0;
            TotalCredits = SelectedCourses?.Sum(c => c.Credit) ?? 0;
        }

        public void UpdateRecommendedCoursesStatus()
        {
            var selectedCourseIds = new HashSet<int>(SelectedCourses.Select(c => c.CourseID));

            foreach (var course in RecommendedBasicCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }
            foreach (var course in RecommendedMajorCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }
            foreach (var course in RecommendedElectiveCourses)
            {
                course.IsSelected = selectedCourseIds.Contains(course.CourseID);
            }

            UpdateStatistics();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}