#!/bin/bash
sudo service nginx start
sudo chmod 777 /home/ubuntu/Cohere/Cohere.Api
sudo systemctl enable CohereAPIMonitor.service
sudo systemctl start CohereAPIMonitor.service

isExistMongoDB=`pgrep mongod`
if [[ -z  $isExistMongoDB ]]; then
    sudo service mongod start
fi