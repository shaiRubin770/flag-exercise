namespace FlagExercise.Common.Models;

public record TxStatusDto(
    bool Running,
    string Machine,
    int FlagsCreated,
    int FilesMoved,
    int Errors,
    DateTime NextFlagAtUtc);

public record RxStatusDto(
    bool Running,
    string Machine,
    int FilesDeleted,
    int Errors);
