#!/bin/bash
if [ -d /home/ubuntu/Cohere ];
  then
    echo "/home/ubuntu/Cohere exists. Deleting..."
    sudo rm -r /home/ubuntu/Cohere
  else
    echo "/home/ubuntu/Cohere is not exist"
fi