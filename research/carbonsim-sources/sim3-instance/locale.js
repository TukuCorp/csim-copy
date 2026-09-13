// For CommonJS or browser.
/* global window */

(function() {
  var stringFormat = typeof require !== 'undefined'
    ? require('StringFormat')
    : typeof window !== 'undefined'
    ? window.StringFormat
    : null;

  var locale = function(currentCultureName, currency, strings) {
    var r = {};
    r.currentCultureName = String(currentCultureName || '');
    r.currency = currency || {};
    r.strings = strings || (typeof (Proxy) == 'function' ? new Proxy({}, { get: /*(t, k) => k*/ function() { return ''; } }) : {});
    r.format = stringFormat;

    r.getLocalEntryFrom = function(localesObj) {
      var segments = r.currentCultureName.split('-');
      for (var ii = segments.length; ii >= 0; ii--) {
        var entry = localesObj[segments.slice(0, ii).join('-')];
        if (entry)
          return entry;
      }
      return null;
    };
    r.getLocalString = function(objWithLocales, memberFunc) {
      if (!objWithLocales)
        return null;
      var locales = objWithLocales.Locales || '{}';
      var localEntry = r.getLocalEntryFrom(JSON.parse(locales)) || {};
      return memberFunc(localEntry) || memberFunc(objWithLocales);
    };
    r.getLocalName = function(objWithLocales) {
      return this.getLocalString(objWithLocales, function(x) { return x.Name; });
    };

    r.CultureInfo = {
      MonthShortNames: function(monthz) {
        return [
          r.strings.Month_Jan, r.strings.Month_Feb, r.strings.Month_Mar, r.strings.Month_Apr, r.strings.Month_May, r.strings.Month_Jun,
          r.strings.Month_Jul, r.strings.Month_Aug, r.strings.Month_Sep, r.strings.Month_Oct, r.strings.Month_Nov, r.strings.Month_Dec
        ][monthz];
      },
      DateUnits: {
        zh: { SeqNo: '\u7B2C', Year: '\u5E74', Month: '\u6708', Day: '\u65E5' }
      }
    };
    r.CultureInfo.DateFormats = {
      /* Similar to .NET "'Year 'yyyy" format */
      Year: {
        zh: function(y) { return r.CultureInfo.DateUnits.zh.SeqNo + (y + 1) + r.CultureInfo.DateUnits.zh.Year; },
        '': function(y) { return r.strings.Year + " " + (y + 1); }
      },
      /* Similar to .NET "M" format */
      MonthDay: {
        zh: function(d) { return (d.getMonth() + 1) + r.CultureInfo.DateUnits.zh.Month + d.getDate() + r.CultureInfo.DateUnits.zh.Day; },
        '': function(d) { return d.getDate() + " " + r.CultureInfo.MonthShortNames(d.getMonth()); }
      },
      /* Similar to .NET "D" format */
      GameLong: {
        zh: function(y, d) {
          return r.CultureInfo.DateUnits.zh.SeqNo + (y + 1) + r.CultureInfo.DateUnits.zh.Year + (d.getMonth() + 1) + r.CultureInfo.DateUnits.zh.Month + d.getDate() + r.CultureInfo.DateUnits.zh.Day;
        },
        '': function(y, d) { return r.CultureInfo.MonthShortNames(d.getMonth()) + " " + d.getDate() + ", " + r.strings.Year + " " + (y + 1); }
      }
    };
    r.CultureInfo.DateFormats.MonthDayBE = {
      zh: r.CultureInfo.DateFormats.MonthDay.zh,
      '': function(d) { return r.CultureInfo.MonthShortNames(d.getMonth()) + " " + d.getDate(); }
    };
    r.CultureInfo.DurationFormats = {
      MonthAndDay: {
        '': function(month, day) { return month + " " + r.strings.Months + " " + day + " " + r.strings.Days; }
      },
      DayAndHour: {
        '': function(day, hour) { return day + " " + r.strings.Days + " " + hour + " " + r.strings.Hours; }
      },
      HourAndMinute: {
        '': function(hour, minute) { return hour + " " + r.strings.Hours + " " + minute + " " + r.strings.Minutes; }
      },
      MinuteAndSecond: {
        '': function(minute, second) { return minute + " " + r.strings.Minutes + " " + second + " " + r.strings.Seconds; }
      }
    }

    r.formatDateYear = r.getLocalEntryFrom(r.CultureInfo.DateFormats.Year);
    r.formatDateMonthDay = r.getLocalEntryFrom(r.CultureInfo.DateFormats.MonthDay);
    r.formatDateMonthDayBE = r.getLocalEntryFrom(r.CultureInfo.DateFormats.MonthDayBE);
    r.formatDateGameLong = r.getLocalEntryFrom(r.CultureInfo.DateFormats.GameLong);

    r.formatDurationMonthAndDay = r.getLocalEntryFrom(r.CultureInfo.DurationFormats.MonthAndDay);
    r.formatDurationDayAndHour = r.getLocalEntryFrom(r.CultureInfo.DurationFormats.DayAndHour);
    r.formatDurationHourAndMinute = r.getLocalEntryFrom(r.CultureInfo.DurationFormats.HourAndMinute);
    r.formatDurationMinuteAndSecond = r.getLocalEntryFrom(r.CultureInfo.DurationFormats.MinuteAndSecond);

    r.getCurrencySymbol = function() {
      return r.currency.Symbol;
    };

    r.getCurrencyPerTCO2eSymbol = function() {
      return r.format(r.strings.X_Per_Y, r.getCurrencySymbol(), r.strings.tCO2e);
    };
    var formatCurrency = function(digits, capital) {
      return (r.toCurrentCurrency(capital))
        .toLocaleString(undefined, { minimumFractionDigits: digits, maximumFractionDigits: digits });
    };
    r.formatCurrencyRate = formatCurrency.bind(null, 2);
    r.formatCurrencyBalance = formatCurrency.bind(null, 0);
    r.formatCurrencyRateWithSymbol = function(capital) {
      return r.getCurrencySymbol() + r.formatCurrencyRate(capital);
    };
    r.formatCurrencyBalanceWithSymbol = function(capital) {
      return r.getCurrencySymbol() + r.formatCurrencyBalance(capital);
    };
    r.parseCurrency = function(currencyValue) {
      return Number(currencyValue) / r.currency.ExchangeRate;
    };
    r.toCurrentCurrency = function(simoleans) {
      return Number(simoleans) * r.currency.ExchangeRate;
    }

    r.formatNumber = function(value, digits) {
      return Number(value).toLocaleString(undefined, { minimumFractionDigits: digits, maximumFractionDigits: digits });
    }

    return r;
  };


  if (typeof module !== 'undefined' && typeof module.exports !== 'undefined')
    module.exports = locale;
  else if (typeof window !== 'undefined')
    window.Locale = locale;
})();
