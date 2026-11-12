CREATE TABLE dbo.Autorizacoes
(
    Id              UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_Autorizacoes PRIMARY KEY,
    IdempotencyKey  NVARCHAR(100)     NOT NULL,
    TerminalId      INT               NOT NULL,
    Valor           DECIMAL(18, 2)    NOT NULL,
    Bandeira        NVARCHAR(20)      NOT NULL,
    CartaoToken     UNIQUEIDENTIFIER  NOT NULL,
    Status          NVARCHAR(20)      NOT NULL,
    CriadaEm        DATETIMEOFFSET(3) NOT NULL,
    PublicadaEm     DATETIMEOFFSET(3) NULL,
    ConfirmadaEm    DATETIMEOFFSET(3) NULL,

    CONSTRAINT UQ_Autorizacoes_IdempotencyKey UNIQUE (IdempotencyKey),
    CONSTRAINT CK_Autorizacoes_Status CHECK (Status IN (N'Pendente', N'Confirmada')),
    CONSTRAINT CK_Autorizacoes_Valor CHECK (Valor > 0)
);
