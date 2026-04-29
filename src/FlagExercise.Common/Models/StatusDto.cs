namespace FlagExercise.Common.Models;

/// <summary>Status snapshot returned by the Tx worker for the UI.</summary>
public record TxStatusDto(
    bool Running,
    string Machine,
    int FlagsCreated,
    int FilesMoved,
    int Errors,
    DateTime NextFlagAtUtc,
    AppConfig Config);

/// <summary>Status snapshot returned by the Rx worker for the UI.</summary>
public record RxStatusDto(
    bool Running,
    string Machine,
    int FilesDeleted,
    int Errors,
    AppConfig Config);
