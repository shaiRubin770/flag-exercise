namespace FlagExercise.Common.Models;

public record TxStatusDto(
    bool Running,
    string Machine,
    int FlagsCreated,
    int FilesMoved,
    int Errors,
    DateTime NextFlagAtUtc,
    AppConfig Config);

public record RxStatusDto(
    bool Running,
    string Machine,
    int FilesDeleted,
    int Errors,
    AppConfig Config);
