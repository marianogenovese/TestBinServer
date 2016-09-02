// node test-tcp.js server=192.168.100.5:8001
var net = require('net');

var ip;
var port;

process.argv.forEach(function (val, index, array) {
	val = val.toLowerCase();
	if(val.startsWith("server=")){
		var params = val.split("=")[1].split(":");
		ip = params[0];
		port = params[1];
		console.log("ip ", ip, " port ", port);
	}
});

var socket = new net.Socket();
socket.connect(port, ip, function(){
	socket.setEncoding('utf-8');

	socket.on("data",function(buffer){
		console.log(buffer);
	});
	
	socket.on("close", function(buffer){
		console.log("Disconect!");
	});
});
