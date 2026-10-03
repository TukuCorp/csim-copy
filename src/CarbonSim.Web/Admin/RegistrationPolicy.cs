namespace CarbonSim.Web.Admin;

/// <summary>
/// The administrator's registration gate, held while the host runs rather than read once at
/// start-up: whether visitors may claim a company at all, and the access PIN they must quote.
/// It begins from the host's configuration, so a deployment that sets a PIN there behaves exactly
/// as before; the administrator console is what moves it afterwards.
/// </summary>
public sealed class RegistrationPolicy
{
    private readonly object _lock = new();

    private string _pin;
    private bool _open = true;

    public RegistrationPolicy(CarbonSimHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _pin = options.RegistrationPin;
    }

    /// <summary>The PIN a visitor must quote; empty means registration cannot open.</summary>
    public string Pin
    {
        get
        {
            lock (_lock)
            {
                return _pin;
            }
        }
    }

    /// <summary>Whether the administrator is letting people register.</summary>
    public bool Open
    {
        get
        {
            lock (_lock)
            {
                return _open;
            }
        }
    }

    /// <summary>Both values together, so a caller decides on one consistent picture.</summary>
    public (bool Open, string Pin) Current
    {
        get
        {
            lock (_lock)
            {
                return (_open, _pin);
            }
        }
    }

    public void SetPin(string pin)
    {
        lock (_lock)
        {
            _pin = pin?.Trim() ?? string.Empty;
        }
    }

    public void SetOpen(bool open)
    {
        lock (_lock)
        {
            _open = open;
        }
    }
}
