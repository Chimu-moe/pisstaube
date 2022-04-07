#!/bin/sh

wrk2 -t 8 -c 10 -d 10s -R 50 -s wrk.lua http://localhost:62011 -L > result.txt

