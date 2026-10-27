namespace Nuskha.Domain;

/// <summary>Pending = awaiting review, Rejected = reviewed and declined (the submitter sees the reason), Hidden = withdrawn by staff.</summary>
public enum BookStatus { Pending = 0, Published = 1, Hidden = 2, Rejected = 3 }

public enum ReadingStatus { Reading = 0, Completed = 1, PlanToRead = 2, OnHold = 3, Dropped = 4 }

/// <summary>Things a user can like or comment on. Comments/likes are polymorphic: (TargetType, TargetId).</summary>
public enum TargetType { Profile = 0, Book = 1, ListEntry = 2, Activity = 3, ForumPost = 4, ForumThread = 5, Comment = 6 }

public enum ActivityType { AddedToList = 0, StatusChanged = 1, Rated = 2, Completed = 3, ThreadCreated = 4, StatusPost = 5 }

public enum NotificationType { Comment = 0, Reply = 1, Like = 2, Follow = 3, Message = 4, BookApproved = 5, BookRejected = 6 }

public enum ReportStatus { Open = 0, Resolved = 1, Dismissed = 2 }
