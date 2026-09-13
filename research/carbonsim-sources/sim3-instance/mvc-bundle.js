if (!$.fn.dataTable.render.text) {
  $.fn.dataTable.render.text = function() {
    return {
      display: function(d) {
        return typeof d === 'string' ? d.replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;') : d;
      }
    };
  };
}

$.fn.dataTable.defaults.column.mRender = $.fn.dataTable.render.text();
;

function formatNumber(num) {
  return Number(num).toLocaleString(undefined, { maximumFractionDigits: 2 });
}

function formatCurrencyNoSymbol(num) {
  return Number(num).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function formatCurrencyWithSymbol(num) {
  return Strings.CurrencySymbol + formatCurrencyNoSymbol(num);
}

function DataTableRenderFormatNumber(data, type) {
  return ('display' == type)
    ? formatNumber(data)
    : data;
}

function DataTableRenderFormatCurrencyNoSymbol(data, type) {
  return ('display' == type)
    ? formatCurrencyNoSymbol(data)
    : data;
}

function DataTableRenderFormatCurrencyWithSymbol(data, type) {
  return ('display' == type)
    ? formatCurrencyWithSymbol(data)
    : data;
}

function getQueryValue(name) {
  return _.find(
    location.search.substr(1)
    .split('&')
    .map(function(q) {
      var p = q.split('=');
      return p.length >= 2 && decodeURIComponent(p[0]) == name
        ? decodeURIComponent(p[1])
        : null;
    }),
    function(p) { return p != null });
}

function encodeHTML(text) {
  return $('<span/>').text(text).html();
}
;
$(function() {
  $("#sortable1, #sortable2").sortable({
    connectWith: ".connectedSortable"
  }).disableSelection();
});
;
$(document).ready(function() {
  $("#renameUnitButton").click(function(e) {
    e.preventDefault();
    var $option = $('#units-list option:selected');
    $option.text($("#new-unit-name").val());
  });
});
;
(function(jQuery) {

  jQuery.eventEmitter = {
    _JQInit: function() {
      this._JQ = jQuery(this);
    },
    emit: function(evt, data) {
      !this._JQ && this._JQInit();
      this._JQ.trigger(evt, data);
    },
    once: function(evt, handler) {
      !this._JQ && this._JQInit();
      this._JQ.one(evt, handler);
    },
    on: function(evt, handler) {
      !this._JQ && this._JQInit();
      this._JQ.bind(evt, handler);
    },
    off: function(evt, handler) {
      !this._JQ && this._JQInit();
      this._JQ.unbind(evt, handler);
    }
  };


  function GameEventer() {
    // do stuff
  }

  jQuery.extend(GameEventer.prototype, jQuery.eventEmitter);

  window.GameEvents = new GameEventer();

}(jQuery));

var getDurationDisplayString = function(seconds) {
  var duration = moment.duration(seconds, 'seconds');
  var months = duration.months();
  var days = duration.days();
  var hours = duration.hours();
  var minutes = duration.minutes();
  var secondsRemainder = duration.seconds();

  var displayString = "";
  if (months > 0) {
    displayString = months + " months " + days + " days";
  } else if (days > 0) {
    displayString = days + " days " + hours + " hours";
  } else if (hours > 0) {
    displayString = hours + " hours " + minutes + " minutes";
  } else {
    displayString = minutes + " minutes " + secondsRemainder + " seconds";
  }

  return displayString;
}



//Sync/Fetch the Timer values

//these vars match the json bag from /home/time
var GlobalGameTime = { GameYear: 0, GameYearSeconds: 0, SecondsPerGameYear: 0, State: 0, initial: true };
var secondsInAYear = 31536000; //seconds in a real life year
var Scale = 1; //This gets set once we know what SecondPerGameYear is
var AllowanceAuctionScheduleData;
var currentAllowanceAuction = { StartTime: 0, EndTime: 0, ScheduleIndex: 0, State: 0, NextStartTime: 0, isLastInYear: 0 };
var realtimeclock = { millisecondsOffset: 0, editing: false };
var remainingSeconds = 0;
var last30sAlert, lastStartAlert;
var padNumber = function(number) {
  if (number <= 0) {
    return "00";
  }
  if (number < 10) {
    return "0" + number;
  }
  return number;
};
var updateRealTimeClock = function() {
  if (realtimeclock.editing == false && $('#clock-timer .timer').length > 0) {
    var now = new Date();
    var utcDate = new Date(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), now.getUTCHours(), now.getUTCMinutes(), now.getUTCSeconds());
    var utcTime = utcDate.getTime();
    var d = new Date(utcTime + realtimeclock.millisecondsOffset);

    $('#clock-timer .timer').html(padNumber(d.getHours()) + ":" + padNumber(d.getMinutes()));
  }
};

var getRealTimeClockData = function() {
  if ($('#clock-timer').length < 1)
    return;
  AjaxPartialEvent("/realtimeclock/gettime",
    null,
    function(data) {
      if (data && data.data) {
        realtimeclock.millisecondsOffset = data.data.secondsOffset * 1000;
        updateRealTimeClock();
      }
    }
  );
};

var getAllowanceAuctionScheduleData = function() {
  if ($('#allowance-auction-timer-text').length < 1)
    return;

  AjaxPartialEvent("/allowanceauctionschedule",
    null,
    function(data) {
      if ($.isArray(data) && data.length > 0) {
        AllowanceAuctionScheduleData = data[0];
        initAllowanceAuctionTimer();
      }
    }
  );
};

var initAllowanceAuctionTimer = function(startIndex) {

  startIndex = typeof startIndex !== 'undefined' ? startIndex : 0;

  //handle case where timer hasn't been started yet.
  if (typeof AllowanceAuctionScheduleData === 'undefined' || AllowanceAuctionScheduleData == null) {
    currentAllowanceAuction.StartTime = 0;
    currentAllowanceAuction.EndTime = 0;
    currentAllowanceAuction.ScheduleIndex = 0;
    currentAllowanceAuction.State = 0;
    currentAllowanceAuction.NextStartTime = 0;
    return;
  }
  var lastIndexForThisYear = 0;
  var currentGameYearSeconds = GlobalGameTime.SecondsPerGameYear - remainingSeconds;
  for (var index = startIndex; index < AllowanceAuctionScheduleData.length; ++index) {
    var allowanceAuction = AllowanceAuctionScheduleData[index];
    if ((allowanceAuction.Vintage.replace(/[^\d]/g, '') - 1) == GlobalGameTime.GameYear) {
      lastIndexForThisYear = index;
      if (allowanceAuction.GameYearSecondsOpen <= currentGameYearSeconds) {
        if (allowanceAuction.GameYearSecondsClose > currentGameYearSeconds) {
          //this is the current allowance auction and it's active
          currentAllowanceAuction.StartTime = allowanceAuction.GameYearSecondsOpen;
          currentAllowanceAuction.EndTime = allowanceAuction.GameYearSecondsClose;
          currentAllowanceAuction.ScheduleIndex = index;
          currentAllowanceAuction.State = 1;
          continue;
        }
        if (allowanceAuction.GameYearSecondsClose <= currentGameYearSeconds) {
          //this may be the current allowance auction but it would have ended by now
          currentAllowanceAuction.StartTime = allowanceAuction.GameYearSecondsOpen;
          currentAllowanceAuction.EndTime = allowanceAuction.GameYearSecondsClose;
          currentAllowanceAuction.ScheduleIndex = index;
          currentAllowanceAuction.State = 0;
          continue;
        }

      }

      if (allowanceAuction.GameYearSecondsOpen > currentGameYearSeconds) {
        //This allowance auction hasn't started yet, so we are either inbetween auctions, or still in the previous auction.
        currentAllowanceAuction.NextStartTime = allowanceAuction.GameYearSecondsOpen;
        break;
      }
    }
  }

  currentAllowanceAuction.isLastInYear = (lastIndexForThisYear == currentAllowanceAuction.ScheduleIndex);

};



var initGameTimers = function(eventHubProxy) {
  eventHubProxy.on('StartYear',
    function(now, year) {
      getRealTimeClockData();
      getAllowanceAuctionScheduleData();
    });

  getRealTimeClockData();
  getAllowanceAuctionScheduleData();

  var addAuctionToastMessage = function(msg) {
    if (window.isAdmin) {
      addHtmlToastMessage("<a href='/Admin/AllowanceAuction'>" + msg + "</a>");
    } else {
      addHtmlToastMessage("<a href='/AllowanceAuction'>" + msg + "</a>");
    }
  };

  var updateAllowanceAuctiontimer = function() {
    var currentGameYearSeconds = GlobalGameTime.SecondsPerGameYear - remainingSeconds;
    var allowanceAuctionRemainingSeconds = 0;
    if (currentAllowanceAuction.State == 1) {

      $('#allowance-auction-timer-text').closest('.panel').removeClass("panel-red");

      $('#allowance-auction-timer-text').text(Strings.AllowanceAuctionRemaining);
      allowanceAuctionRemainingSeconds = currentAllowanceAuction.EndTime - currentGameYearSeconds;
      // Use to fire "ends in 30 seconds" alert
      if (allowanceAuctionRemainingSeconds > 0 && (Math.abs(allowanceAuctionRemainingSeconds - 30) < 2) && (!last30sAlert || (currentGameYearSeconds - last30sAlert) > 30)) {
        addAuctionToastMessage(Strings.AuctionPreEndAlert);
        last30sAlert = currentGameYearSeconds;
      }
      if (allowanceAuctionRemainingSeconds <= 0) {
        addAuctionToastMessage(Strings.AuctionEndAlert);
        initAllowanceAuctionTimer(currentAllowanceAuction.ScheduleIndex);
      }
    } else if (currentAllowanceAuction.State == 0) {
      if (currentAllowanceAuction.isLastInYear) {
        $('#allowance-auction-timer-text').text(Strings.AllowanceAuctionTradingHalted);
        $('#allowance-auction-timer-text').closest('.panel').addClass("panel-red");
      } else {
        $('#allowance-auction-timer-text').closest('.panel').removeClass("panel-red");
        $('#allowance-auction-timer-text').text(Strings.AllowanceAuctionStartsIn);
      }
      if (currentAllowanceAuction.NextStartTime <= currentGameYearSeconds) {
        // Fire auction started alert
        if (currentAllowanceAuction.NextStartTime > 0 && (!lastStartAlert || currentAllowanceAuction.NextStartTime > lastStartAlert)) {
          lastStartAlert = currentAllowanceAuction.NextStartTime;
          addAuctionToastMessage(Strings.AuctionStartAlert);
        }

        initAllowanceAuctionTimer(currentAllowanceAuction.ScheduleIndex);
      } else {
        allowanceAuctionRemainingSeconds = currentAllowanceAuction.NextStartTime - currentGameYearSeconds;
        // Use to fire "starts in 30 seconds" alert
        if (allowanceAuctionRemainingSeconds > 0 && (Math.abs(allowanceAuctionRemainingSeconds - 30) < 2) && (!last30sAlert || (currentGameYearSeconds - last30sAlert) > 31)) {
          addAuctionToastMessage(Strings.AuctionPreStartAlert);
          last30sAlert = currentGameYearSeconds;
        }
      }
    }

    if (allowanceAuctionRemainingSeconds >= 0) {
      $('#allowance-auction-timer').text(getDurationDisplayString(allowanceAuctionRemainingSeconds));
    }
  };



  var updateExchangeMarketTimer = function() {
    if (remainingSeconds > 0) {
      $('#secondary-market-timer-text').closest('.panel').removeClass("panel-red");
      $('#secondary-market-timer-text').text(Strings.SecondaryMarketRemaining);
      $('#secondary-market-timer').text(getDurationDisplayString(remainingSeconds));
    } else {
      $('#secondary-market-timer-text').closest('.panel').addClass("panel-red");
      $('#secondary-market-timer-text').text(Strings.SecondaryMarketTradingHalted);
    }
  };

  var secondsBetweenSync = 10;
  //how many seconds to pass before polling the server clock again.
  var nextSyncCounter = secondsBetweenSync;
  var syncGameTime = function() {
    AjaxPartialEvent("/home/time",
      null,
      function(data) {
        // Diff check
        if (!GlobalGameTime.initial) {
          if (data.State != GlobalGameTime.State) {
            GameEvents.emit('statechange', { from: GlobalGameTime.State, to: data.State });
            if (data.State == 1) {
              GameEvents.emit('run', { from: GlobalGameTime.State });
              getRealTimeClockData();
              getAllowanceAuctionScheduleData();
            } else if (data.State == 2) {
              GameEvents.emit('pause', { from: GlobalGameTime.State });
            } else if (data.State == 3) {
              GameEvents.emit('tradinghalted', { from: GlobalGameTime.State });
            } else if (data.State == 4) {
              GameEvents.emit('yearended', { from: GlobalGameTime.State });
            }
          }
        } else {
          if (data.State == 4) {
            $('#lbbutton').show();
          }
        }

        GlobalGameTime = data;
        remainingSeconds = Math.max(data.SecondsPerGameYear - data.GameYearSeconds, 0);
        Scale = secondsInAYear / GlobalGameTime.SecondsPerGameYear;
      }
    );
  };
  syncGameTime();


  setInterval(function() {

      updateRealTimeClock();
      if (typeof GlobalGameTime === 'undefined' || GlobalGameTime == null || GlobalGameTime.SecondsPerGameYear == 0) {
        return;
      }

      //var minutes = padNumber(Math.floor(remainingSeconds / 60));
      //var seconds  = padNumber(Math.floor(remainingSeconds % 60));

      $('#time-left-timer .timer').text(getDurationDisplayString(remainingSeconds));

      var d = new Date("1970-01-01T00:00:00");

      if (remainingSeconds < 1) {
        d.setSeconds((GlobalGameTime.SecondsPerGameYear - 1) * Scale);
      } else {
        d.setSeconds((GlobalGameTime.SecondsPerGameYear - remainingSeconds) * Scale);
      }
      $('#year-remaining-timer').text(formatDateGameLong(GlobalGameTime.GameYear, d));

      updateAllowanceAuctiontimer();
      updateExchangeMarketTimer();

      if (GlobalGameTime.State == 1 && remainingSeconds > 0) { //Running
        --remainingSeconds;
      }
      --nextSyncCounter;
      if (nextSyncCounter == 0) {
        syncGameTime();
        nextSyncCounter = secondsBetweenSync;
      }
    },
    1000);

  $('#clock-timer .edit').on('click',
    function() {
      realtimeclock.editing = true;
      var now = new Date();
      var utcDate = new Date(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), now.getUTCHours(), now.getUTCMinutes(), now.getUTCSeconds());
      var utcTime = utcDate.getTime();
      var d = new Date(utcTime + realtimeclock.millisecondsOffset);

      $('#clock-timer .edit').hide();
      $('#clock-timer .set').show();
      $('#clock-timer .timer').html('<form><input name="clock-set-hour" class="clock-set-hour" type="number" min="0" max="23" value="'
        + padNumber(d.getHours())
        + '" />:<input name="clock-set-minutes" class="clock-set-minutes" type="number" min="0" max="59" value="'
        + padNumber(d.getMinutes())
        + '" /></form>');

    });
  $('#clock-timer .set').on('click',
    function() {

      $('#clock-timer .set').hide();
      $('#clock-timer .edit').show();

      AjaxPartialEvent("/realtimeclock/settime",
        null,
        function(data) {
          if (data && data.data) {
            realtimeclock.millisecondsOffset = data.data.secondsOffset * 1000;
            updateRealTimeClock();
          }
        },
        function() {
          var now = new Date();
          var hoursOffset = $('.clock-set-hour').val() - now.getUTCHours();
          var minutesOffset = $('.clock-set-minutes').val() - now.getUTCMinutes();
          return { 'minutesOffset': ((60 * hoursOffset) + (minutesOffset)) };
        }
      );

      realtimeclock.editing = false;
      updateRealTimeClock();
    });


};
;
/*
* functions which make form(s) with mvc unobtrusive validation compatible with bootstrap error styling.
*/



var oldErrorFunction;

function setupUnobtrusiveBootstrap(form, modelStateErrors, hideGeneralErrorOnSubmit) {
  if (form.length == 0)
    return;

  //Fix for forms loaded as ajax partials. Remove validator and add back again
  form.removeData('validator');
  form.removeData('unobtrusiveValidation');
  $.validator.unobtrusive.parse(form);


  if (hideGeneralErrorOnSubmit) {
    form.bind("submit",
      function() {
        var validationFailAlert = form.find('.validationFailAlert');
        if (validationFailAlert.length > 0)
          validationFailAlert.addClass("hidden");
      });
  }
  form.bind("invalid-form.validate",
    function() {
      var nearestForm = $(this).closest('form');
      showValidationFailAlert(nearestForm);
    });

  form.each(function(index, item) {

    var validator = $.data(item, 'validator');

    if (validator != null) {
      var settngs = validator.settings;

      var oldErrorFunction = settngs.errorPlacement;

      settngs.errorPlacement = function(error, inputElement) {
        depictError(error.text(), inputElement, form);
        if (oldErrorFunction)
          oldErrorFunction(error, inputElement);
      };
    }
  });
  displayErrorsFromModelState(form, modelStateErrors);
}

function displayGeneralError(form, message) {
  hideSuccess(form);
  var generalError = form.find('.general-error');
  generalError.text(message);
  generalError.show();
  form.find('.form-errors').removeClass("hidden");
}

function displaySuccess(form, message, withScroll) {
  var $form = $(form);
  var $success = $form.find('.form-success');
  $success.text(message);
  $success.removeClass("hidden");
  hideErrors(form);
  if (withScroll)
    $success.scrollIntoView();
}

function hideSuccess(form) {
  var $form = $(form);
  var $success = $form.find('.form-success');
  $success.addClass("hidden");
}

function hideErrors(form) {
  var $form = $(form);
  // get validator object
  var validator = $form.validate();

  // get errors that were created using jQuery.validate.unobtrusive
  var errors = $form.find(".field-validation-error span");

  // trick unobtrusive to think the elements were succesfully validated
  // this removes the validation messages
  errors.each(function() { validator.settings.success($(this)); });

  var generalError = $form.find('.general-error');
  generalError.hide();

  $form.find('.validationFailAlert').addClass("hidden");
  $form.find('.form-errors').addClass("hidden");
}

function displayErrorsFromModelState(form, modelStateErrors) {
  var $form = $(form);
  //Check we have errors from server
  if (modelStateErrors == null || modelStateErrors.length == 0)
    return;
  var validator = $form.validate();
  var generalError = $form.find('.general-error');
  generalError.hide();
  var errors = {}; //"AllowanceAuctionInterval": "ssssss"

  modelStateErrors.forEach(function(error) {
    if (error.Key == "GeneralError") {
      displayGeneralError($form, error.Message);
      return;
    }
    errors[error.Key] = error.Message;
  });
  validator.showErrors(errors);
  showValidationFailAlert($form);
}


function showValidationFailAlert(form) {
  hideSuccess(form);
  var validationFailAlert = form.find('.validationFailAlert');
  if (validationFailAlert.length > 0)
    validationFailAlert.removeClass("hidden");
  form.find('.form-errors').removeClass("hidden");
}

function depictError(errorMessage, inputElement, form) {
  var formGroup = inputElement.closest(".form-group");
  var fieldMod = formGroup.find(".form-control-feedback");
  if (errorMessage == "") {
    formGroup.removeClass("has-error");
    formGroup.removeClass("has-feedback");
    if (fieldMod.length > 0) {
      fieldMod.addClass("hidden");
    }
  } else {

    formGroup.addClass("has-error");
    formGroup.addClass("has-feedback");
    if (fieldMod.length > 0) {
      fieldMod.removeClass("hidden");
    }
  }

}
;

var bootstrapAlert = function(jqParent, msgText, type) {
  type = type || 'danger';

  var jqAlert =
    $('<div class="alert alert-' + type + ' alert-dismissible" role="alert"><button type="button" class="close" data-dismiss="alert"><span aria-hidden="true">&times;</span></button></div>');
  jqAlert.append(document.createTextNode(msgText));
  jqParent.append(jqAlert);
};

var initAjaxPartialForm = function(jqForm, jqActionArea, onSuccess, customFormData) {
  jqForm.submit(function(event) {
    event.preventDefault();

    if (!$(this).valid()) {
      return;
    }

    if (!customFormData) {
      customFormData = function() {
        return jqForm.serialize();
      };
    }

    $('body').css({ 'cursor': 'progress' });
    jqForm.find('.loaderAnimation').show();
    $.post(jqForm.attr('action'), customFormData())
      .always(function() {
        jqForm.find('.loaderAnimation').hide();
        $('body').css({ 'cursor': 'auto' });
      })
      .fail(function(jqXhr, textStatus, errorThrown) {
        bootstrapAlert(jqForm, Strings.UnableToContactServer + ' [' + Strings.ErrorThrown + ' "' + errorThrown + '"].');
      })
      .done(function(data) {
        if (data.success != null && !data.success) {
          displayErrorsFromModelState(jqForm, data.Errors);
          return;
        }
        if (jqActionArea != null && jqActionArea.length > 0)
          jqActionArea.html(data);
        if (onSuccess)
          onSuccess();
      });
  });
};

var AjaxPartialEvent = function(controllerAction, jqActionArea, onSuccess, customFormData) {

  if (!customFormData) {
    customFormData = function() {
      return "";
    };
  }

  $.post(controllerAction, customFormData())
    .always(function() {
      $('body').css({ 'cursor': 'auto' });
    })
    .fail(function(jqXhr, textStatus, errorThrown) {
    })
    .done(function(data) {
      if (data.success != null && !data.success) {
        return;
      }
      if (jqActionArea != null && jqActionArea.length > 0)
        jqActionArea.html(data);
      if (onSuccess)
        onSuccess(data);
    });
};

var initAjaxPartialButton = function(jqButton, jqActionArea, onSuccess) {
  jqButton.click(function(event) {
    event.preventDefault();

    $('body').css('cursor', 'progress');
    jqButton.append('<img class="loaderAnimation" src="/static/images/loader.gif" style="margin: 5px; vertical-align: top;" />');
    $.get(jqButton.attr('href'), jqButton.serialize())
      .always(function() {
        jqButton.find('.loaderAnimation').remove();
        $('body').css('cursor', 'auto');
      })
      .fail(function(jqXhr, textStatus, errorThrown) {
        bootstrapAlert(jqButton, Strings.UnableToContactServer + ' [' + Strings.ErrorThrown + ' "' + errorThrown + '"].');
      })
      .done(function(data) {
        jqActionArea.html(data);
        if (onSuccess)
          onSuccess();
      });
  });
};

var initAjaxPartialSelect = function(jqForm, jqActionArea, onSuccess) {
  $("select", jqForm).change(function(event) {
    event.preventDefault();

    $('body').css('cursor', 'progress');
    jqForm.append('<img class="loaderAnimation" src="/static/images/loader.gif" style="margin: 5px; vertical-align: top;" />');
    $.post(jqForm.attr('action'), jqForm.serialize())
      .always(function() {
        jqForm.find('.loaderAnimation').remove();
        $('body').css('cursor', 'auto');
      })
      .fail(function(jqXhr, textStatus, errorThrown) {
        bootstrapAlert(jqForm, Strings.UnableToContactServer + ' [' + Strings.ErrorThrown + ' "' + errorThrown + '"].');
      })
      .done(function(data) {
        jqActionArea.html(data);
        if (onSuccess)
          onSuccess();
      });
  });
};

var processForm = function(form, onResult, customFormData, onFail) {
  var jqForm = $(form);
  jqForm.submit(function(event) {
    event.preventDefault();

    if ($(this).valid && !$(this).valid()) {
      return false;
    }

    customFormData = customFormData
      || function() {
        return jqForm.serialize();
      };


    $('body').css({ 'cursor': 'progress' });
    jqForm.find('.loaderAnimation').show();

    $.post(jqForm.attr('action'), customFormData())
      .always(function() {
        jqForm.find('.loaderAnimation').hide();
        $('body').css({ 'cursor': 'auto' });
      })
      .fail(onFail
        || function(jqXhr, textStatus, errorThrown) {
          displayGeneralError(jqForm, Strings.UnableToContactServer);
          console.log(Strings.UnableToContactServer + ' [' + Strings.ErrorThrown + ' "' + errorThrown + '"].');
        })
      .done(function(data) {
        onResult(data, jqForm);
      });
    return false;
  });

}

var setValidityFromAjaxException = function(jqInput, jqXhr) {
  // Display modelstate errors
  var fieldErrors = jqXhr && jqXhr.responseJSON && jqXhr.responseJSON.errors && jqXhr.responseJSON.errors.filter(function(o) { return o.hasOwnProperty('Key') && o.hasOwnProperty('Message') });
  if (fieldErrors && fieldErrors.length > 0) {
    var elForm = jqInput[0].form;
    fieldErrors.forEach(function(fieldError) {
      var elField = elForm[fieldError.Key];
      if (elField) {
        elField.setCustomValidity(fieldError.Message);
        elField.form.reportValidity();
        $(elField).one('input change',
          function() {
            elField.setCustomValidity('');
          });
      }
    });
    return;
  }

  // Display general error
  var msg = (jqXhr && jqXhr.responseJSON && jqXhr.responseJSON.error) || Strings.UnableToContactServer;
  var elInput = jqInput[0];
  elInput.setCustomValidity(msg);
  elInput.form.reportValidity();
  $(elInput.form).one('input change',
    function() {
      elInput.setCustomValidity('');
    });
};
;
function showimagepreview(input, preview) {
  if (input.files && input.files[0]) {
    preview.toggle(true);
    var filerdr = new FileReader();
    filerdr.onload = function(e) {
      preview.attr('src', e.target.result);
    };
    filerdr.readAsDataURL(input.files[0]);
  }
}
;
var initLocaleEditor = function(idHidden, propertyName, idAppendTo, idInputElement, controlClass) {

  // Element identification
  var dataFor = idHidden + '.' + propertyName;
  var dataForFilter = function() { return dataFor == this.getAttribute('data-for'); };

  // Element lookup
  var jqHidden = $(document.getElementById(idHidden));
  var jqAppendTo = $(document.getElementById(idAppendTo));
  if (0 == jqHidden.length || 0 == jqAppendTo)
    throw 'initLocaleEditor() failed - elements missing';
  var jqSelect = jqAppendTo.find('select').filter(dataForFilter).first();
  var jqInput = jqAppendTo.find('input').filter(dataForFilter).first();

  // Behaviours
  var populateInput = function() {
    try {
      jqInput.val(JSON.parse(jqHidden.val())[jqSelect.val()][propertyName]);
    } catch (e) {
      jqInput.val('');
    }
  };
  var applyEdits = function() {
    var dataObj;
    try {
      dataObj = JSON.parse(jqHidden.val());
    } catch (e) {
      dataObj = {};
    }
    var cultureName = jqSelect.val();
    if (!dataObj[cultureName])
      dataObj[cultureName] = {};
    dataObj[cultureName][propertyName] = jqInput.val();
    jqHidden.val(JSON.stringify(dataObj));
  };

  // Init?
  if (0 == jqSelect.length && 0 == jqInput.length) {
    // Init select
    jqSelect = $(document.createElement('select'));
    jqSelect.attr('data-for', dataFor);
    jqSelect.addClass('locale-editor');
    if (controlClass)
      jqSelect.addClass(controlClass);
    var optCount = 0;
    for (var name in window.DataTranslationCultures) {
      var option = $(document.createElement('option'));
      option.attr('value', name);
      option.append(document.createTextNode(window.DataTranslationCultures[name]));
      jqSelect.append(option);
      optCount++;
    }
    if (optCount < 2) {
      jqSelect.prop('disabled', true);
    }
    jqAppendTo.append(jqSelect);
    if (optCount < 1) {
      return;
    }

    // Init input
    jqInput = $(document.createElement('input'));
    jqInput.attr('data-for', dataFor);
    if (idInputElement)
      jqInput.attr('id', idInputElement);
    jqInput.addClass('locale-editor');
    if (controlClass)
      jqInput.addClass(controlClass);
    jqSelect.after(jqInput);

    // Initially and on selection change, populate input.
    jqSelect.on('focus', applyEdits);
    jqSelect.on('change', populateInput);

    // On edit, update JSON
    jqInput.on('propertychange change click keyup input paste', applyEdits);
  }

  // Init or re-init value from JSON
  if (0 != jqSelect.length && 0 != jqInput.length) {
    populateInput();
  }
};

var initBootstrapLocaleEditor = function(idHidden, propertyName, idAppendTo, idInputElement) {
  initLocaleEditor(idHidden, propertyName, idAppendTo, idInputElement, 'form-control');
};
;

var filterWithinStdDev = function(array, valueSelector, deviations) {
  deviations = deviations || 1;

  var avg = _(array).reduce(function(acc, element) {
        return acc + valueSelector(element);
      },
      0)
    / array.length;

  var stddev = Math.sqrt(
    _(array).reduce(function(acc, element) {
        return acc + Math.pow(valueSelector(element) - avg, 2);
      },
      0)
    / array.length
  );

  var filtCeil = avg + stddev * deviations;
  var filtFloor = avg - stddev * deviations;
  return _(array).filter(function(element) {
    return valueSelector(element) >= filtFloor
      && valueSelector(element) <= filtCeil;
  });
};

//# sourceMappingURL=mvc-bundle.js.map
