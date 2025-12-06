
CREATE DATABASE StudentClubDB
Go
USE StudentClubDB
-- ==================================
-- 1. USERS TABLE
-- ==================================
CREATE TABLE Users (
    UserID INT IDENTITY PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) UNIQUE NOT NULL,
    Phone NVARCHAR(20),
    Avatar NVARCHAR(MAX) NULL,  
    PasswordHash VARBINARY(256) NOT NULL,
    Role NVARCHAR(20)
        CHECK (Role IN ('Student','ClubLeader','Admin')) NOT NULL,
    AccountStatus NVARCHAR(20)
        CHECK (AccountStatus IN ('PendingVerification','Active','Locked','Disabled'))
        DEFAULT 'PendingVerification',
    CreatedAt DATETIME DEFAULT GETDATE(),
    LastLogin DATETIME NULL
);


CREATE TABLE UserTokens (
    TokenID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    Token NVARCHAR(200) UNIQUE NOT NULL,
    TokenType NVARCHAR(50)
        CHECK (TokenType IN ('Activation','ResetPassword','Refresh')),
    ExpiryDate DATETIME NOT NULL,
    IsUsed BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);


CREATE TABLE Clubs (
    ClubID INT IDENTITY PRIMARY KEY,
    ClubName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX),
    PresidentID INT NULL,
    Status NVARCHAR(20)
        CHECK (Status IN ('Active','Inactive','Pending','Suspended'))
        DEFAULT 'Active',
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (PresidentID) REFERENCES Users(UserID)
);


CREATE TABLE ClubMembers (
    MemberID INT IDENTITY PRIMARY KEY,
    ClubID INT NOT NULL,
    UserID INT NOT NULL,
    JoinedDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(20)
        CHECK (Status IN ('Pending','Approved','Rejected','Removed'))
        DEFAULT 'Pending',
    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);


CREATE TABLE Activities (
    ActivityID INT IDENTITY PRIMARY KEY,
    ClubID INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX),
    ActivityDate DATETIME NOT NULL,
    Status NVARCHAR(20)
        CHECK (Status IN ('Scheduled','Completed','Cancelled'))
        DEFAULT 'Scheduled',
    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID)
);


CREATE TABLE FeeSchedule (
    FeeScheduleID INT IDENTITY PRIMARY KEY,
    ClubID INT NOT NULL,
    FeeName NVARCHAR(200) NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    DueDate DATE NOT NULL,
    Frequency NVARCHAR(20)
        CHECK (Frequency IN ('OneTime','Monthly','Yearly')),
    Status NVARCHAR(20)
        CHECK (Status IN ('Active','Inactive'))
        DEFAULT 'Active',
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID)
);

CREATE TABLE Fees (
    FeeID INT IDENTITY PRIMARY KEY,
    FeeScheduleID INT NOT NULL,
    ClubID INT NOT NULL,
    UserID INT NOT NULL,
    OrderCode INT NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    FeeType NVARCHAR(50)
        CHECK (FeeType IN ('Membership','Activity','Other')),
    PaymentStatus NVARCHAR(20)
        CHECK (PaymentStatus IN ('Pending','Paid','Overdue','Cancelled'))
        DEFAULT 'Pending',
    CreatedAt DATETIME DEFAULT GETDATE(),
    PaidAt DATETIME NULL,

    FOREIGN KEY (FeeScheduleID) REFERENCES FeeSchedule(FeeScheduleID),
    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);


CREATE TABLE ApprovalRequests (
    RequestID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    ClubID INT NOT NULL,
    RequestType NVARCHAR(50)
        CHECK (RequestType IN ('JoinClub','LeaveClub')),
    Status NVARCHAR(20)
        CHECK (Status IN ('Pending','Approved','Rejected'))
        DEFAULT 'Pending',
    Note NVARCHAR(MAX),
    CreatedAt DATETIME DEFAULT GETDATE(),
    ApprovedAt DATETIME NULL,
    FOREIGN KEY (UserID) REFERENCES Users(UserID),
    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID)
);


CREATE TABLE Notifications (
    NotificationID INT IDENTITY PRIMARY KEY,
    UserID INT NOT NULL,
    Title NVARCHAR(200) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    IsRead BIT DEFAULT 0,
    CreatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);

CREATE TABLE ClubImages (
    ImageID INT IDENTITY PRIMARY KEY,
    ClubID INT NOT NULL,
    UserID INT NOT NULL,         
    ImageURL NVARCHAR(MAX) NOT NULL, 
    Caption NVARCHAR(MAX) NULL,
    ImageType NVARCHAR(50)
        CHECK (ImageType IN ('Post','Cover','Event','Other'))
        DEFAULT 'Post',
    Status NVARCHAR(20)
        CHECK (Status IN ('Active','Deleted'))
        DEFAULT 'Active',
    CreatedAt DATETIME DEFAULT GETDATE(),

    FOREIGN KEY (ClubID) REFERENCES Clubs(ClubID),
    FOREIGN KEY (UserID) REFERENCES Users(UserID)
);



USE StudentClubDB
GO

-- =============================================
-- 1. THÊM USER MẪU (sinh viên + chủ nhiệm + CLB leader + Admin)
-- =============================================
SET IDENTITY_INSERT Users ON
GO

INSERT INTO Users (UserID, FullName, Email, Phone, Avatar, PasswordHash, Role, AccountStatus, CreatedAt, LastLogin) VALUES
-- Admin
(1, N'Nguyễn Văn Admin', 'admin@fpt.edu.vn', '0901234567', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, -- pass: 123456
    'Admin', 'Active', '2024-01-01', '2025-08-10'),

-- Chủ nhiệm CLB (ClubLeader)
(2, N'Trần Thị Minh Thư', 'thu.ttm@fpt.edu.vn', '0987654321', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'ClubLeader', 'Active', '2024-02-15', '2025-08-09'),
(3, N'Lê Hoàng Nam', 'nam.lh@fpt.edu.vn', '0912345678', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'ClubLeader', 'Active', '2024-03-01', '2025-08-08'),
(4, N'Phạm Ngọc Ánh', 'anh.pn@fpt.edu.vn', '0934567890', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'ClubLeader', 'Active', '2024-04-10', '2025-08-07'),

-- Sinh viên thường
(5, N'Nguyễn Văn A', 'a.nv@fpt.edu.vn', '0901112223', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-01', '2025-08-10'),
(6, N'Trần Thị Bích', 'bich.tt@fpt.edu.vn', '0911223344', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-02', '2025-08-09'),
(7, N'Hoàng Văn Cường', 'cuong.hv@fpt.edu.vn', '0922334455', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-03', '2025-08-08'),
(8, N'Đỗ Thị Diễm', 'diem.dt@fpt.edu.vn', '0933445566', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-04', NULL),
(9, N'Phạm Minh Đức', 'duc.pm@fpt.edu.vn', '0944556677', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-05', '2025-08-07'),
(10, N'Vũ Thị Hương', 'huong.vt@fpt.edu.vn', '0955667788', NULL, 
    0x24326224313224316E6A724B6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C654C6A724C65, 'Student', 'Active', '2024-09-06', NULL);

SET IDENTITY_INSERT Users OFF
GO

-- =============================================
-- 2. CLUBS MẪU (rất thật FPT)
-- =============================================
SET IDENTITY_INSERT Clubs ON
GO

INSERT INTO Clubs (ClubID, ClubName, Description, PresidentID, Status, CreatedAt) VALUES
(1, N'FPTU Dance Club - FDC', N'CLB nhảy hiện đại, Kpop, Hiphop, Flashmob...', 2, 'Active', '2024-03-01'),
(2, N'FPTU English Club - FEC', N'CLB tiếng Anh - giao tiếp, thi IELTS, debate...', 3, 'Active', '2024-02-20'),
(3, N'FPTU IT Club - FIC', N'CLB Công nghệ thông tin - lập trình, AI, hackathon...', 4, 'Active', '2024-04-01'),
(4, N'FPTU Guitar Club - FGC', N'CLB Guitar - biểu diễn, dạy chơi guitar...', 2, 'Active', '2024-05-10'),
(5, N'FPTU Volunteer Club - FVC', N'CLB tình nguyện - thiện nguyện, hiến máu...', 3, 'Active', '2024-06-15');

SET IDENTITY_INSERT Clubs OFF
GO

-- =============================================
-- 3. CLUB MEMBERS (thành viên đã duyệt)
-- =============================================
INSERT INTO ClubMembers (ClubID, UserID, JoinedDate, Status) VALUES
(1, 2, '2024-03-01', 'Approved'), -- Minh Thư làm chủ tịch FDC
(1, 5, '2024-09-10', 'Approved'),
(1, 6, '2024-09-11', 'Approved'),
(1, 8, '2024-09-12', 'Approved'),

(2, 3, '2024-02-20', 'Approved'), -- Hoàng Nam chủ tịch FEC
(2, 7, '2024-09-15', 'Approved'),
(2, 9, '2024-09-16', 'Approved'),

(3, 4, '2024-04-01', 'Approved'), -- Ngọc Ánh chủ tịch FIC
(3, 5, '2024-09-20', 'Approved'),
(3, 10, '2024-09-21', 'Approved'),

(4, 2, '2024-05-10', 'Approved'), -- Minh Thư cũng chơi guitar
(4, 6, '2024-09-25', 'Approved'),

(5, 3, '2024-06-15', 'Approved'); -- Hoàng Nam làm tình nguyện

-- =============================================
-- 4. ACTIVITIES (sự kiện sắp tới & đã qua)
-- =============================================
SET IDENTITY_INSERT Activities ON
GO

INSERT INTO Activities (ActivityID, ClubID, Title, Description, ActivityDate, Status) VALUES
(1, 1, N'FDC Flashmob 2025', N'Biểu diễn tại lễ khai mạc năm học mới', '2025-09-20 14:00:00', 'Scheduled'),
(2, 1, N'Lớp nhảy Kpop cơ bản', N'Dạy miễn phí cho thành viên mới', '2025-08-25 18:00:00', 'Scheduled'),
(3, 2, N'English Speaking Contest 2025', N'Thi nói tiếng Anh cấp trường', '2025-10-15 08:00:00', 'Scheduled'),
(4, 2, N'IELTS Workshop', N'Hướng dẫn làm bài thi IELTS', '2025-08-30 09:00:00', 'Scheduled'),
(5, 3, N'Hackathon FPT 2025', N'Lập trình 48h liên tục', '2025-11-01 00:00:00', 'Scheduled'),
(6, 3, N'Seminar AI & Machine Learning', N'Mời chuyên gia từ FPT Software', '2025-08-20 14:00:00', 'Completed');

SET IDENTITY_INSERT Activities OFF
GO

-- =============================================
-- 5. FEE SCHEDULE (khoản thu định kỳ)
-- =============================================
SET IDENTITY_INSERT FeeSchedule ON
GO

INSERT INTO FeeSchedule (FeeScheduleID, ClubID, FeeName, Amount, DueDate, Frequency, Status, CreatedAt) VALUES
(1, 1, N'Phí thành viên FDC 2025', 150000, '2025-09-15', 'Yearly', 'Active', '2025-08-01'),
(2, 2, N'Phí tham gia FEC', 100000, '2025-09-10', 'OneTime', 'Active', '2025-08-01'),
(3, 3, N'Phí hội viên FIC', 200000, '2025-09-20', 'Yearly', 'Active', '2025-08-01'),
(4, 1, N'Phí học nhảy nâng cao', 300000, '2025-10-01', 'OneTime', 'Active', '2025-08-15');

SET IDENTITY_INSERT FeeSchedule OFF
GO

-- =============================================
-- 6. FEES (khoản phải nộp của từng người)
-- =============================================
INSERT INTO Fees (FeeScheduleID, ClubID, UserID, OrderCode, Amount, FeeType, PaymentStatus, CreatedAt, PaidAt) VALUES
(1, 1, 5, 1001, 150000, 'Membership', 'Paid', '2025-08-10', '2025-08-12'),
(1, 1, 6, 1002, 150000, 'Membership', 'Paid', '2025-08-10', '2025-08-11'),
(1, 1, 8, 1003, 150000, 'Membership', 'Pending', '2025-08-10', NULL),
(2, 2, 7, 2001, 100000, 'Membership', 'Paid', '2025-08-15', '2025-08-16'),
(3, 3, 5, 3001, 200000, 'Membership', 'Pending', '2025-08-20', NULL);

-- =============================================
-- 7. NOTIFICATIONS (thông báo mẫu)
-- =============================================
SET IDENTITY_INSERT Notifications ON
GO

INSERT INTO Notifications (NotificationID, UserID, Title, Message, IsRead, CreatedAt) VALUES
(1, 5, N'Chào mừng đến FDC!', N'Bạn đã gia nhập FPTU Dance Club thành công!', 1, '2025-08-10'),
(2, 5, N'Phí thành viên FDC 2025', N'Bạn cần đóng 150.000đ trước ngày 15/09/2025', 1, '2025-08-10'),
(3, 8, N'Phí thành viên FDC 2025', N'Bạn cần đóng 150.000đ trước ngày 15/09/2025', 0, '2025-08-10'),
(4, 5, N'Sự kiện sắp tới', N'Flashmob 2025 - 20/09/2025. Đừng bỏ lỡ!', 0, '2025-08-15'),
(5, 7, N'English Speaking Contest', N'Đăng ký trước 01/10/2025 nhé!', 0, '2025-08-20');

SET IDENTITY_INSERT Notifications OFF
GO

PRINT 'THÊM DỮ LIỆU MẪU THÀNH CÔNG! BẠN ĐÃ CÓ HỆ THỐNG HOÀN CHỈNH SIÊU ĐẸP!'