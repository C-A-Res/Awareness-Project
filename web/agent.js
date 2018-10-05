// TODO 1080 x 1920

function init() {

  var ws;

  // Animation
  var avatarState;

  var ball = document.getElementById("ball");
  var shadow = document.getElementById("shadow");
  var eye1 = document.getElementById("eye1");
  var eye2 = document.getElementById("eye2");

  var stateLabel = document.getElementById("state");

  // Controls
  var close = document.getElementById("btnClose");

  var connected = document.getElementById("connected");
  var listening = document.getElementById("listening");
  var thinking = document.getElementById("thinking");


  function animateAvitar(state) {
    if (state != avatarState) {
      thinking.style.backgroundColor = "yellow";
      ball.style.animation = "none";
      shadow.style.animation = "none";
      ball.offsetHeight;
      shadow.offsetHeight;
      ball.style.animation = null;
      shadow.style.animation = null;
      listening.style.backgroundColor = "red";
    switch(state) {
        case "speaking":
          stateLabel.innerHTML = "Speaking";
          eye1.classList.remove('eye-down');
          eye1.classList.remove('eye-sleep');
          eye1.classList.add('eye');
          eye2.classList.remove('eye-down');
          eye2.classList.remove('eye-sleep');
          eye2.classList.add('eye');
          ball.style.animation = "bounce 2s ease-out 2";
          shadow.style.animation = "shadow-bounce 2s ease-out 2";
          thinking.style.backgroundColor = "maroon";
          break;
        case "thinking":
          stateLabel.innerHTML = "Thinking";
          eye1.classList.remove('eye');
          eye1.classList.remove('eye-sleep');
          eye1.classList.add('eye-down');
          eye2.classList.remove('eye');
          eye2.classList.remove('eye-sleep');
          eye2.classList.add('eye-down');
          ball.style.animation = "lil-bounce 2s linear infinite";
          shadow.style.animation = "shadow-lil-bounce 2s linear infinite";
          thinking.style.backgroundColor = "orange";
          break;
        case "listening":
          stateLabel.innerHTML = "Listening";
          eye1.classList.remove('eye-down');
          eye1.classList.remove('eye-sleep');
          eye1.classList.add('eye');
          eye2.classList.remove('eye-down');
          eye2.classList.remove('eye-sleep');
          eye2.classList.add('eye');
          //ball.style.transition = "transform 1s";
          ball.style.animation = "breathe 3s linear infinite";
          shadow.style.animation = "none";
          listening.style.backgroundColor = "green";
          break;
        case "sleeping":
          resetScreen();
          stateLabel.innerHTML = "Sleeping";
          eye1.classList.remove('eye');
          eye1.classList.remove('eye-down');
          eye1.classList.add('eye-sleep');
          eye2.classList.remove('eye');
          eye2.classList.remove('eye-down');
          eye2.classList.add('eye-sleep');
          ball.style.animation = "to-roll 1s, roll 5s linear infinite";
          //ball.style.animationName = "roll";
          //ball.style.WebkitAnimationName = "roll";  //browser compatibility
          //ball.style.animationIterationCount = "infinite";
          shadow.style.animation = "shadow-roll 5s linear infinite";
          //shadow.style.animationName = "shadow-roll";
          //shadow.style.WebkitAnimationName = "shadow-roll";  //browser compatibility
          //shadow.style.animationIterationCount = "infinite";
          break;
        default:
          output("unknown state: " + state)

      }
      avatarState = state;
    }
  }

  function updateScroll() {
    $("#chatSpace").scrollTop($("#chatSpace")[0].scrollHeight);
  }

  function displayText(user, text) {
    var newTextNode = document.createTextNode(text);
    var textContainer = document.createElement("div");
    if (user == "kiosk") {
      textContainer.className = 'newbotDiv'; 
    } else {
      textContainer.className = 'newuserDiv';
    }
    textContainer.appendChild(newTextNode);
    document.getElementById('chatSpace').appendChild(textContainer);    
    updateScroll();
  }

  function displayMap(name, id) {
    mapView.style.display = "block";
    buttonView.style.display = "none";
    calendarView.style.display = "none";
    inputView.style.display = "none";

    youarehere.style.left = calculateX("kiosk");
    youarehere.style.top = calculateY("kiosk") - 4;

    dest_m.innerHTML = name;

    if (name == "You") {
      dest_m.style.left = calculateX("kiosk") - 2;
      dest_m.style.top = calculateY("kiosk") - 8;

      dest.style.left = calculateX("kiosk");
      dest.style.top = calculateY("kiosk") - 4;
    } else {
      
      dest_m.style.left = calculateX(id);
      dest_m.style.top = calculateY(id);

      dest.style.left = calculateX(id);
      dest.style.top = calculateY(id) + 4;
    }
    
  }

  // used to transform from mapData x coordinates to this map coordinates
  function calculateX (id) {
    // sample from data map 3005: 246, 56
    // sample from this map 3005: 155, 122
    // sample from data map 3017: 458, 56
    // sample from this map 3017: 392, 122
    var xDataSampleDelta = 212.;
    var xThisSampleDelta = 237.;
    var xDataToThisScale = xThisSampleDelta / xDataSampleDelta;

    var xDataOffset = 139.;
    var xThisOffset = 35.;
    // var xDataEnd = 959;
    // var xThisEnd = 952;
    return (mapData[id].x - xDataOffset) * xDataToThisScale + xThisOffset;
  }

  // used to transform from mapData y coordinates to this map coordinates
  function calculateY (id) {
    // sample from data map 3107: 180, 226
    // sample from this map 3107: 83, 317
    // sample from data map 3123: 180, 571
    // sample from this map 3123: 83, 317
    var yDataSampleDelta = 345.;
    var yThisSampleDelta = 396.;
    var yDataToThisScale = yThisSampleDelta / yDataSampleDelta;

    var yDataOffset = 5.;
    var yThisOffset = 63.;
    // var yDataEnd = 747;
    // var yThisEnd = 915;
    return (mapData[id].y - yDataOffset) * yDataToThisScale + yThisOffset;
  }

  function displayCalendar() {
    calendarView.style.display = "block";
    buttonView.style.display = "none";
    mapView.style.display = "none";
    inputView.style.display = "none";
  }

  function displayKeyboard() {
    buttonView.style.display = "none";
    mapView.style.display = "none";
    calendarView.style.display = "none";
    inputView.style.display = "block";
  }

  function displayStarterButtons() {
    buttonView.style.display = "block";
    inputView.style.display = "none";
    mapView.style.display = "none";
    calendarView.style.display = "none";
  }

  function resetScreen() {
    displayStarterButtons();
    var cloned = chatSpace.cloneNode(false);
    chatSpace.parentNode.replaceChild(cloned, chatSpace);
    input.value = "";
  }

  //$.ajax({ url: "https://www.mccormick.northwestern.edu/images/research-and-faculty/directory/forbus-ken.jpg",
  //  type: "POST",
  //  crossDomain: true,
  //});

  animateAvitar("sleeping");

  var btnSleeping = document.getElementById("btnSleeping");
  btnSleeping.onclick= function() { animateAvitar('sleeping')}
  var btnThinking = document.getElementById("btnThinking");
  btnThinking.onclick= function() { animateAvitar("thinking")}
  var btnListening = document.getElementById("btnListening");
  btnListening.onclick= function() { animateAvitar("listening")}
  var btnSpeaking = document.getElementById("btnSpeaking");
  btnSpeaking.onclick= function() { animateAvitar("speaking")}

  // touch input
  //var input = document.getElementById("input");
  var btnwhere = document.getElementById("where");
  btnwhere.onclick = function() { 
    input.value = "Where is ?";
    displayKeyboard();
    ws.send(":wake");
  } 
  var btnwhen = document.getElementById("when");
  btnwhen.onclick = function() { 
    input.value = "When is ?";
    displayKeyboard();
    ws.send(":wake");
  } 
  var btnwhat = document.getElementById("what");
  btnwhat.onclick = function() { 
    input.value = "What is ?";
    displayKeyboard();
    ws.send(":wake");
  } 
  var btnother = document.getElementById("other");
  btnother.onclick = function() { 
    displayKeyboard();
    ws.send(":wake");
  } 

  var keyButtons = document.getElementsByClassName("keyButton");
  for (var i = 0; i < keyButtons.length; i++) {
    element = keyButtons[i];
    element.onclick = function() {
      text = input.value;
      if (text.endsWith('?')) {
        text = text.substring(0,text.length-1) + this.innerHTML + '?';  
      } else {
        text = text + this.innerHTML;  
      }
      input.value = text;
    }
  }

  var touchButtons = document.getElementsByClassName("touchButton2");
  for (var i = 0; i < touchButtons.length; i++) {
    element = touchButtons[i];
    element.onclick = function() {
      text = input.value;
      if (text.endsWith('?')) {
        text = text.substring(0,text.length-1) + this.getAttribute("data-value") + '?';
      } else {
        text = text + this.getAttribute("data-value") + " ";
      }
      input.value = text;
    }
  }

  backspace.onclick = function() {
    text = input.value;
    if (text.endsWith('?')) {
        input.value = text.substring(0,text.length-2) + '?';
    } else {
      input.value = text.substring(0,text.length-1);
    }
  }

  showmap.onclick = function() {
    displayMap("", "0");
  }
  
  $(".backButton").click(function () {
    input.value = "";
    displayStarterButtons();
  });

  $("#showCalendar").click(function () {
    displayCalendar();
  });

  // FOR TESTING
  $("#mapDestinationSubmit").click(function () {
    console.log("room: " + mapDestination.value);
    dest.style.left = calculateX(mapDestination.value);
    dest.style.top = calculateY(mapDestination.value);
  });

  // Connect to Web Socket
  var isConnected = false;
  var connector = setInterval(connectToServer,3000);

  var poller;

  function connectToServer() {
    ws = new WebSocket("ws://localhost:9791/dialog");
    wsx = ws;
    stateLabel.innerHTML = "Connecting...";

    // this isn't working
    close.setAttribute("onClick", "ws.close()");

    // Set event handlers.
    ws.onopen = function() {
      output("onopen");
      connected.style.backgroundColor = "darkblue";
      isConnected = true;
      clearInterval(connector);
      poller = setInterval(pollPsi, 500);

      avatarState = "none";
    };
    
    ws.onmessage = function(e) {
      // e.data contains received string.
      output("onmessage: " + e.data);
      json_data = JSON.parse(e.data);
      for (i = 0; i < json_data.length; i++) {
        command = json_data[i].command;
        args = json_data[i].args;
        switch(command) {
          case "displayText":
            user = args.user;
            text = args.content;
            displayText(user, text);
            break;
          case "setAvatarState":
            animateAvitar(args);
            break;
          case "debug":
            output(args);
            break;
          case "displayMap":
            displayMap(args.name, args.id);
            break;
          case "displayCalendar":
            displayCalendar();
            break;
          default:
            output("Unrecognized command: " + command);
        }
      }

    };
    
    ws.onclose = function() {
      output("onclose");
      connected.style.backgroundColor = "gray";
      if (isConnected) {
        clearInterval(poller);
        //connector = setInterval(connectToServer,3000);
        stateLabel.innerHTML = "Disconnected";
      }
      isConnected = false;
    };

    ws.onerror = function(e) {
      output("onerror");
    };

    function pollPsi() {
      ws.send('');
    }

    $("#submit").click(function() {
      text = input.value;
      output("sending: " + text);
      //displayText("user", text);
      wsx.send(text);
      input.value = "";
      displayStarterButtons();
      return false;
    });

  }

  $(".toggleDebug").click(function() {
    $("#debug").toggle("slow", function(){});
    $(".toggleDebug").toggleClass("debugOn");
  });

}