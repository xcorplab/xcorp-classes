public sealed class MeuContext : DbContext, IUnitOfWork
{
    private bool _rolledback;

    public DbContextTransaction Transaction { get; private set; }

    public void Commit()
    {
        if (Transaction != null && !_rolledback)
        {
            Transaction.Commit();
            Transaction.Dispose();
            Transaction = null;
        }
    }

    public DbContextTransaction StartTransaction()
        => Transaction ?? (Transaction = Database.BeginTransaction());

    public DbContextTransaction StartTransaction(IsolationLevel isolationLevel)
        => Transaction ?? (Transaction = Database.BeginTransaction(isolationLevel));

    public void Rollback()
    {
        if (Transaction?.UnderlyingTransaction.Connection != null && !_rolledback)
        {
            Transaction.Rollback();
            _rolledback = true;
        }
    }

    public void Save()
    {
        try
        {
            ChangeTracker.DetectChanges();
            SaveChanges();
        }
        catch (DbEntityValidationException exception)
        {
            Rollback();
            throw new EntidadeInvalidaException(string.Join(Environment.NewLine, exception.EntityValidationErrors.SelectMany(e => e.ValidationErrors).Select(e => $"{e.PropertyName} => {e.ErrorMessage}")));
        }
        catch
        {
            Rollback();
            throw;
        }
    }

    public void SaveAndCommit()
    {
        try
        {
            Save();
#if !NCRUNCH
            Commit();
#endif
        }
        catch
        {
            //Dispose não deve lançar exceção
        }
    }

    public void SaveAndCommitAndStartTransaction()
    {
        SaveAndCommit();
        StartTransaction();
    }

    protected override void Dispose(bool disposing)
    {
        if (_rolledback) Rollback();
        else if (Transaction != null && !_rolledback) SaveAndCommit();

        base.Dispose(disposing);
    }
}