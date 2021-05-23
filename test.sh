#!/bin/sh

for test in $(ls -rc tests); do
    echo "================================"
    test_data=$(cat tests/$test)
    result=${test:0:1}
    count=$(echo $test_data | wc -m)
    echo -n "$test: "
    if [ $count -le 100 ]; then
        echo $test_data
    else
        echo "too long"
    fi
    mono vjp.exe < tests/$test
    if [ $? -ne 0 ]; then
        if [ $result == 'n' ] || [ $result == 'i' ]; then
            echo "PASSED"
        else
            echo "FAILED"
        fi
    else
        if [ $result == 'y' ] || [ $result == 'i' ]; then
            echo "PASSED"
        else
            echo "FAILED"
        fi
    fi
done
