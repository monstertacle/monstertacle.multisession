CREATE TABLE Metadata (
    Singleton INTEGER PRIMARY KEY CHECK (Singleton = 1),
    SchemaVersion INTEGER NOT NULL,
    MigrationVersion INTEGER NOT NULL,
    BindingEncodingVersion INTEGER NOT NULL,
    StoreId TEXT NOT NULL,
    InstanceTrustScopeId TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    DispatchMode INTEGER NOT NULL CHECK (DispatchMode IN (0, 1))
) STRICT;
CREATE TABLE Sessions (
    SessionId TEXT PRIMARY KEY NOT NULL,
    ProviderId TEXT NOT NULL,
    AgentProfileId TEXT NOT NULL,
    ModelId TEXT NOT NULL,
    AdapterId TEXT,
    ContractVersion INTEGER,
    SettingsRevision TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL,
    ClosedUtc TEXT,
    Lifecycle INTEGER NOT NULL,
    BlockReason INTEGER NOT NULL,
    BindingRevision INTEGER NOT NULL CHECK (BindingRevision >= 0),
    NextSequence INTEGER NOT NULL CHECK (NextSequence >= 1),
    SnapshotJson TEXT NOT NULL
) STRICT;
CREATE INDEX SessionOrder ON Sessions (CreatedUtc, SessionId);
CREATE TABLE Requests (
    RequestId TEXT PRIMARY KEY NOT NULL,
    SessionId TEXT NOT NULL REFERENCES Sessions(SessionId),
    Sequence INTEGER NOT NULL CHECK (Sequence >= 1),
    AcceptedUtc TEXT NOT NULL,
    StartedUtc TEXT,
    EndedUtc TEXT,
    CancellationRequestedUtc TEXT,
    State INTEGER NOT NULL,
    Certainty INTEGER NOT NULL,
    DispatchId TEXT UNIQUE,
    BindingRevision INTEGER,
    SubmissionJson TEXT NOT NULL,
    SnapshotJson TEXT NOT NULL,
    ReservedDispatchJson TEXT,
    UNIQUE (SessionId, Sequence)
) STRICT;
CREATE INDEX QueueOrder ON Requests (State, AcceptedUtc, RequestId);
CREATE TABLE Results (
    RequestId TEXT PRIMARY KEY NOT NULL REFERENCES Requests(RequestId),
    CompletedUtc TEXT NOT NULL,
    ReplyJson TEXT NOT NULL
) STRICT;
CREATE TABLE History (
    HistoryId TEXT PRIMARY KEY NOT NULL,
    SessionId TEXT NOT NULL REFERENCES Sessions(SessionId),
    RequestId TEXT NOT NULL REFERENCES Requests(RequestId),
    Sequence INTEGER NOT NULL,
    HistoryOrdinal INTEGER NOT NULL CHECK (HistoryOrdinal >= 1),
    SnapshotJson TEXT NOT NULL,
    UNIQUE (SessionId, HistoryOrdinal)
) STRICT;
CREATE TABLE Reservations (
    DispatchId TEXT PRIMARY KEY NOT NULL,
    RequestId TEXT NOT NULL UNIQUE REFERENCES Requests(RequestId),
    SessionId TEXT NOT NULL REFERENCES Sessions(SessionId),
    WorkspaceKey TEXT,
    ReservedUtc TEXT NOT NULL,
    ReleasedUtc TEXT,
    ReleaseReason TEXT
) STRICT;
CREATE UNIQUE INDEX ActiveSessionReservation ON Reservations (SessionId) WHERE ReleasedUtc IS NULL;
CREATE UNIQUE INDEX ActiveWorkspaceReservation ON Reservations (WorkspaceKey) WHERE ReleasedUtc IS NULL AND WorkspaceKey IS NOT NULL;
CREATE TABLE RecoveryCases (
    TargetKind INTEGER NOT NULL,
    TargetId TEXT NOT NULL,
    SessionId TEXT NOT NULL REFERENCES Sessions(SessionId),
    RequestId TEXT REFERENCES Requests(RequestId),
    DispatchId TEXT REFERENCES Reservations(DispatchId),
    Revision INTEGER NOT NULL CHECK (Revision >= 1),
    CreatedUtc TEXT NOT NULL,
    Status INTEGER NOT NULL,
    SnapshotJson TEXT NOT NULL,
    PRIMARY KEY (TargetKind, TargetId)
) STRICT;
CREATE INDEX RecoveryOrder ON RecoveryCases (Status, CreatedUtc, TargetKind, TargetId);
CREATE TABLE RecoveryDecisions (
    DecisionId TEXT PRIMARY KEY NOT NULL,
    TargetKind INTEGER NOT NULL,
    TargetId TEXT NOT NULL,
    AppliedUtc TEXT NOT NULL,
    DecisionJson TEXT NOT NULL,
    FOREIGN KEY (TargetKind, TargetId) REFERENCES RecoveryCases(TargetKind, TargetId)
) STRICT;
